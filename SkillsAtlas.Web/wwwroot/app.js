const form = document.querySelector("#repository-form");
const repositoryInput = document.querySelector("#repository-input");
const scanButton = document.querySelector("#scan-button");
const buttonLabel = scanButton.querySelector(".button-label");
const loadingState = document.querySelector("#loading-state");
const errorState = document.querySelector("#error-state");
const results = document.querySelector("#results");
const welcome = document.querySelector("#welcome");
const skillList = document.querySelector("#skill-list");
const filterInput = document.querySelector("#skill-filter");
const noFilterResults = document.querySelector("#no-filter-results");
const singleModeButton = document.querySelector("#single-mode");
const multipleModeButton = document.querySelector("#multiple-mode");
const addRepositoryButton = document.querySelector("#add-repository");
const repositoryList = document.querySelector("#repository-list");
let skills = [];
let databasePath = "";
let expandedSkill = null;
let similarSkillsAreStale = true;
let repositoryMode = "single";
let repositories = [];

setupCursorCat();
setupFireballs();

const libraryTab = document.querySelector("#library-tab");
const similarTab = document.querySelector("#similar-tab");
const organizationTab = document.querySelector("#organization-tab");
const similarView = document.querySelector("#similar-view");
const organizationView = document.querySelector("#organization-view");
const similarGroups = document.querySelector("#similar-groups");
const similarLoading = document.querySelector("#similar-loading");
const similarError = document.querySelector("#similar-error");
const noSimilarResults = document.querySelector("#no-similar-results");

libraryTab.addEventListener("click", () => switchView("library"));
similarTab.addEventListener("click", () => switchView("similar"));
organizationTab.addEventListener("click", () => switchView("organization"));

const organizationForm = document.querySelector("#organization-form");
const organizationInput = document.querySelector("#organization-input");
const organizationScanButton = document.querySelector("#organization-scan-button");
const organizationLoading = document.querySelector("#organization-loading");
const organizationProgressLabel = document.querySelector("#organization-progress-label");
const organizationProgressCount = document.querySelector("#organization-progress-count");
const organizationProgressBar = document.querySelector("#organization-progress-bar");
organizationForm.addEventListener("submit", scanOrganization);

const initialLoad = loadSavedSkills();

singleModeButton.addEventListener("click", () => setRepositoryMode("single"));
multipleModeButton.addEventListener("click", () => setRepositoryMode("multiple"));
addRepositoryButton.addEventListener("click", addRepository);
repositoryInput.addEventListener("keydown", (event) => {
  if (repositoryMode === "multiple" && event.key === "Enter") {
    event.preventDefault();
    addRepository();
  }
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const selectedRepositories = repositoryMode === "multiple"
    ? repositories
    : [repositoryInput.value.trim()];
  if (selectedRepositories.length === 0) {
    showError("Add at least one repository before searching.");
    repositoryInput.focus();
    return;
  }

  setLoading(true);
  errorState.hidden = true;
  results.hidden = true;
  await initialLoad;

  try {
    const previousCount = skills.length;
    const response = await fetch("/api/skills", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(repositoryMode === "multiple"
        ? { repositories: selectedRepositories }
        : { repository: selectedRepositories[0] })
    });
    const payload = await response.json();
    if (!response.ok) throw new Error(payload.message || "The repository could not be read.");
    const searchedRepositories = payload.searchedRepositories || [];
    const sourceLabel = searchedRepositories.length > 1
      ? `Searched ${searchedRepositories.length} repositories`
      : payload.searchedRepository ? `Searched ${payload.searchedRepository}` : null;
    renderLibrary(payload, sourceLabel, true);
    similarSkillsAreStale = true;
    if (skills.length > previousCount) {
      document.querySelector(".skill-card:last-child")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }
  } catch (error) {
    errorState.textContent = error.message || "Something went wrong while reading that repository.";
    errorState.hidden = false;
  } finally {
    setLoading(false);
  }
});

filterInput.addEventListener("input", () => renderSkills());

async function loadSavedSkills() {
  try {
    const response = await fetch("/api/skills");
    if (!response.ok) throw new Error("The saved skill library could not be loaded.");
    const library = await response.json();
    renderLibrary(library, library.skills?.length ? "Loaded from your SQLite library" : null);
  } catch (error) {
    errorState.textContent = error.message || "The saved skill library could not be loaded.";
    errorState.hidden = false;
  }
}

async function scanOrganization(event) {
  event.preventDefault();
  organizationScanButton.disabled = true;
  organizationLoading.hidden = false;
  organizationProgressLabel.textContent = "Reading organization repositories…";
  organizationProgressCount.textContent = "0 repositories scanned";
  organizationProgressBar.max = 1;
  organizationProgressBar.value = 0;
  errorState.hidden = true;
  switchView("library");

  let streamStarted = false;
  let scanCompleted = false;
  const repositoryErrors = [];
  try {
    await initialLoad;
    const response = await fetch("/api/organizations/scan", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ organizationUrl: organizationInput.value.trim() })
    });
    if (!response.ok) {
      const payload = await response.json().catch(() => ({}));
      throw new Error(payload.message || "The organization could not be scanned.");
    }

    await readOrganizationEvents(response, (message) => {
      if (message.type === "start") {
        streamStarted = true;
        const total = message.totalRepositories;
        organizationProgressBar.max = Math.max(total, 1);
        organizationProgressBar.value = 0;
        organizationProgressLabel.textContent = `Scanning ${message.organizationUrl}`;
        organizationProgressCount.textContent = `0 / ${total} repositories scanned`;
        renderLibrary({ skills, databasePath }, `Scanning ${message.organizationUrl} · 0 / ${total} repositories`, false, true);
        return;
      }

      if (message.type === "repository") {
        similarSkillsAreStale = true;
        mergeSkills(message.skills || []);
        organizationProgressBar.value = message.completedRepositories;
        organizationProgressLabel.textContent = message.error
          ? `Could not scan ${message.repository}`
          : `Scanned ${message.repository}`;
        organizationProgressCount.textContent = `${message.completedRepositories} / ${message.totalRepositories} repositories scanned`;
        if (message.error) repositoryErrors.push(`${message.repository}: ${message.error}`);
        renderLibrary(
          { skills, databasePath },
          `Scanning organization · ${message.completedRepositories} / ${message.totalRepositories} repositories`,
          false,
          true
        );
        return;
      }

      if (message.type === "complete") {
        scanCompleted = true;
        organizationProgressBar.value = Math.max(message.totalRepositories, 1);
        organizationProgressLabel.textContent = "Organization scan complete";
        organizationProgressCount.textContent = `${message.completedRepositories} / ${message.totalRepositories} repositories scanned · ${message.discoveredSkills} skills · ${message.failedRepositories} failed`;
        const summary = `Scanned ${message.completedRepositories} repositories` +
          (message.failedRepositories ? ` · ${message.failedRepositories} could not be scanned` : "");
        renderLibrary({ skills, databasePath }, summary, false, true);
      }
    });

    if (!scanCompleted) throw new Error("The organization scan ended before all repositories were processed.");
    similarSkillsAreStale = true;
    if (repositoryErrors.length) {
      const shownErrors = repositoryErrors.slice(0, 3).join("\n");
      const remainingErrors = repositoryErrors.length - Math.min(repositoryErrors.length, 3);
      errorState.textContent = `${repositoryErrors.length} repositories could not be scanned. ${shownErrors}` +
        (remainingErrors ? `\n…and ${remainingErrors} more.` : "");
      errorState.hidden = false;
    }
  } catch (error) {
    errorState.textContent = error.message || "The organization could not be scanned.";
    errorState.hidden = false;
    if (streamStarted && !scanCompleted)
      organizationProgressLabel.textContent = "The organization scan stopped before completion";
  } finally {
    organizationScanButton.disabled = false;
    if (!streamStarted) organizationLoading.hidden = true;
  }
}

async function readOrganizationEvents(response, onEvent) {
  if (!response.body) throw new Error("This browser could not read the organization scan stream.");
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let pending = "";
  try {
    while (true) {
      const { value, done } = await reader.read();
      pending += decoder.decode(value || new Uint8Array(), { stream: !done });
      let newlineIndex;
      while ((newlineIndex = pending.indexOf("\n")) >= 0) {
        const line = pending.slice(0, newlineIndex).trim();
        pending = pending.slice(newlineIndex + 1);
        if (line) onEvent(JSON.parse(line));
      }
      if (done) break;
    }
    if (pending.trim()) onEvent(JSON.parse(pending));
  } finally {
    reader.releaseLock();
  }
}

function mergeSkills(discoveredSkills) {
  const merged = new Map(skills.map((skill) => [
    JSON.stringify([skill.repositoryUrl, skill.commitHash, skill.name]),
    skill
  ]));
  discoveredSkills.forEach((skill) => {
    merged.set(JSON.stringify([skill.repositoryUrl, skill.commitHash, skill.name]), skill);
  });
  skills = [...merged.values()];
}

function setLoading(isLoading) {
  scanButton.disabled = isLoading;
  addRepositoryButton.disabled = isLoading;
  buttonLabel.textContent = isLoading ? "Searching…" : "Search";
  loadingState.hidden = !isLoading;
  if (isLoading) errorState.hidden = true;
}

function setRepositoryMode(mode) {
  repositoryMode = mode;
  const isMultiple = mode === "multiple";
  singleModeButton.classList.toggle("is-active", !isMultiple);
  multipleModeButton.classList.toggle("is-active", isMultiple);
  singleModeButton.setAttribute("aria-pressed", String(!isMultiple));
  multipleModeButton.setAttribute("aria-pressed", String(isMultiple));
  addRepositoryButton.hidden = !isMultiple;
  repositoryList.hidden = !isMultiple;
  repositoryInput.required = !isMultiple;
  repositoryInput.placeholder = isMultiple
    ? "Paste a repository URL, then press Add"
    : "https://github.com/JetBrains/kotlin";
  errorState.hidden = true;
  repositoryInput.focus();
}

function addRepository() {
  const repository = repositoryInput.value.trim();
  if (!repository) {
    showError("Enter a repository URL to add.");
    repositoryInput.focus();
    return;
  }
  if (repositories.some((item) => item.toLocaleLowerCase() === repository.toLocaleLowerCase())) {
    showError("That repository is already in the list.");
    return;
  }

  repositories.push(repository);
  repositoryInput.value = "";
  errorState.hidden = true;
  renderRepositories();
  repositoryInput.focus();
}

function renderRepositories() {
  repositoryList.replaceChildren();
  repositories.forEach((repository, index) => {
    const item = document.createElement("div");
    item.className = "repository-item";
    const number = document.createElement("span");
    number.className = "repository-item-index";
    number.textContent = String(index + 1).padStart(2, "0");
    const url = document.createElement("span");
    url.className = "repository-item-url";
    url.textContent = repository;
    url.title = repository;
    const remove = document.createElement("button");
    remove.className = "remove-repository";
    remove.type = "button";
    remove.textContent = "×";
    remove.setAttribute("aria-label", `Remove ${repository}`);
    remove.addEventListener("click", () => {
      repositories.splice(index, 1);
      renderRepositories();
    });
    item.append(number, url, remove);
    repositoryList.append(item);
  });
}

function showError(message) {
  errorState.textContent = message;
  errorState.hidden = false;
}

function renderLibrary(library, sourceLabel, shouldScroll = false, preserveExpanded = false) {
  skills = library.skills || [];
  databasePath = library.databasePath || databasePath;
  if (!preserveExpanded) expandedSkill = null;
  const repositoryCount = new Set(skills.map((skill) => skill.repositoryUrl)).size;
  document.querySelector("#repository-meta").textContent = sourceLabel
    ? `${sourceLabel} · ${skills.length} saved ${skills.length === 1 ? "skill" : "skills"} across ${repositoryCount} ${repositoryCount === 1 ? "repository" : "repositories"}`
    : `${skills.length} saved ${skills.length === 1 ? "skill" : "skills"} across ${repositoryCount} ${repositoryCount === 1 ? "repository" : "repositories"}`;
  document.querySelector("#database-location").textContent = databasePath ? `SQLite database: ${databasePath}` : "";
  document.querySelector("#skill-count").textContent = `${skills.length} ${skills.length === 1 ? "skill" : "skills"}`;
  welcome.hidden = true;
  results.hidden = false;
  renderSkills();
  if (shouldScroll) results.scrollIntoView({ behavior: "smooth", block: "start" });
}

function renderSkills() {
  const filter = filterInput.value.trim().toLocaleLowerCase();
  const visibleSkills = skills.filter((skill) =>
    skill.name.toLocaleLowerCase().includes(filter)
  );

  skillList.replaceChildren();
  noFilterResults.textContent = skills.length === 0
    ? "No saved skills yet. Explore a repository to add some."
    : `No skill names match “${filterInput.value.trim()}”.`;
  noFilterResults.hidden = visibleSkills.length !== 0;
  document.querySelector("#skill-count").textContent = filter
    ? `${visibleSkills.length} of ${skills.length} ${skills.length === 1 ? "skill" : "skills"}`
    : `${skills.length} ${skills.length === 1 ? "skill" : "skills"}`;

  visibleSkills.forEach((skill, index) => {
    const originalIndex = skills.indexOf(skill);
    const expanded = expandedSkill === originalIndex;
    const card = document.createElement("article");
    card.className = `skill-card${expanded ? " is-expanded" : ""}`;

    const row = document.createElement("div");
    row.className = "skill-row";

    const toggle = document.createElement("button");
    toggle.className = "skill-main";
    toggle.type = "button";
    toggle.setAttribute("aria-expanded", String(expanded));
    toggle.setAttribute("aria-controls", `skill-detail-${originalIndex}`);

    const number = document.createElement("span");
    number.className = "skill-number";
    number.textContent = String(index + 1).padStart(2, "0");

    const copy = document.createElement("span");
    copy.className = "skill-copy";
    const titleLine = document.createElement("span");
    titleLine.className = "skill-title-line";
    const title = document.createElement("span");
    title.className = "skill-title";
    title.textContent = skill.name;
    const chevron = document.createElement("span");
    chevron.className = "skill-chevron";
    chevron.setAttribute("aria-hidden", "true");
    chevron.textContent = "⌄";
    titleLine.append(title, chevron);
    const description = document.createElement("span");
    description.className = "skill-description";
    description.textContent = skill.shortDescription;
    copy.append(titleLine, description);
    toggle.append(number, copy);
    toggle.addEventListener("click", () => {
      expandedSkill = expandedSkill === originalIndex ? null : originalIndex;
      renderSkills();
    });

    const fileLink = document.createElement("a");
    fileLink.className = "file-widget";
    fileLink.href = skill.fileUrl;
    fileLink.target = "_blank";
    fileLink.rel = "noopener noreferrer";
    fileLink.title = `Open ${skill.relativeFilePath}`;
    const fileIcon = document.createElement("span");
    fileIcon.className = "file-icon";
    fileIcon.setAttribute("aria-hidden", "true");
    fileIcon.textContent = "MD";
    const fileName = document.createElement("span");
    fileName.className = "file-name";
    fileName.textContent = skill.relativeFilePath;
    const external = document.createElement("span");
    external.className = "file-external";
    external.setAttribute("aria-hidden", "true");
    external.textContent = "↗";
    fileLink.append(fileIcon, fileName, external);
    row.append(toggle, fileLink);

    const detailGrid = document.createElement("div");
    detailGrid.className = "detail-grid";
    const detailClip = document.createElement("div");
    detailClip.className = "detail-clip";
    const detail = document.createElement("div");
    detail.className = "skill-detail";
    detail.id = `skill-detail-${originalIndex}`;
    const detailInner = document.createElement("div");
    detailInner.className = "detail-inner";
    const detailLabel = document.createElement("div");
    detailLabel.className = "detail-label";
    detailLabel.textContent = "FULL DESCRIPTION";
    const content = document.createElement("div");
    content.className = "skill-content";
    content.innerHTML = skill.contentHtml || "<p>No additional description is available for this skill.</p>";
    detailInner.append(detailLabel, content);
    detail.append(detailInner);
    detailClip.append(detail);
    detailGrid.append(detailClip);
    card.append(row, detailGrid);
    skillList.append(card);
  });
}

function switchView(view) {
  const showSimilar = view === "similar";
  const showOrganization = view === "organization";
  libraryTab.classList.toggle("is-active", !showSimilar);
  similarTab.classList.toggle("is-active", showSimilar);
  organizationTab.classList.toggle("is-active", showOrganization);
  libraryTab.setAttribute("aria-selected", String(!showSimilar));
  similarTab.setAttribute("aria-selected", String(showSimilar));
  organizationTab.setAttribute("aria-selected", String(showOrganization));
  document.querySelectorAll("main > section:not(#similar-view):not(#organization-view)").forEach((section) => {
    section.classList.toggle("view-hidden", showSimilar || showOrganization);
  });
  similarView.hidden = !showSimilar;
  organizationView.hidden = !showOrganization;
  if (showSimilar && similarSkillsAreStale) loadSimilarSkills();
}

async function loadSimilarSkills() {
  similarLoading.hidden = false;
  similarError.hidden = true;
  noSimilarResults.hidden = true;
  similarGroups.replaceChildren();
  try {
    const response = await fetch("/api/similar-skills");
    if (!response.ok) throw new Error("Similar skills could not be analyzed.");
    const result = await response.json();
    renderSimilarSkills(result);
    similarSkillsAreStale = false;
  } catch (error) {
    similarError.textContent = error.message || "Similar skills could not be analyzed.";
    similarError.hidden = false;
  } finally {
    similarLoading.hidden = true;
  }
}

function renderSimilarSkills(result) {
  const groups = result.groups || [];
  const matchedCount = groups.reduce((total, group) => total + group.skills.length, 0);
  document.querySelector("#similar-summary").textContent = groups.length
    ? `${groups.length} ${groups.length === 1 ? "group" : "groups"} · ${matchedCount} related skills · ${result.analyzedSkillCount} analyzed`
    : `${result.analyzedSkillCount} ${result.analyzedSkillCount === 1 ? "skill" : "skills"} analyzed`;
  noSimilarResults.hidden = groups.length !== 0;

  groups.forEach((group, groupIndex) => {
    const widget = document.createElement("article");
    widget.className = "similar-widget";
    const header = document.createElement("header");
    header.className = "similar-widget-header";
    const label = document.createElement("div");
    label.innerHTML = `<span>GROUP ${String(groupIndex + 1).padStart(2, "0")}</span><strong>${group.skills.length} similar skills</strong>`;
    const score = document.createElement("span");
    score.className = "match-pill";
    score.textContent = `${Math.round(Math.max(...group.skills.map((item) => item.similarity)) * 100)}% strongest match`;
    header.append(label, score);

    const list = document.createElement("div");
    list.className = "similar-widget-list";
    group.skills.forEach((item) => {
      const row = document.createElement("div");
      row.className = "similar-skill";
      const copy = document.createElement("div");
      copy.className = "similar-skill-copy";
      const name = document.createElement("a");
      name.href = item.skill.fileUrl;
      name.target = "_blank";
      name.rel = "noopener noreferrer";
      name.textContent = item.skill.name;
      const repository = document.createElement("span");
      repository.textContent = item.skill.repositoryName;
      const description = document.createElement("p");
      description.textContent = item.skill.shortDescription;
      copy.append(name, repository, description);
      const meter = document.createElement("div");
      meter.className = "similar-meter";
      meter.innerHTML = `<strong>${Math.round(item.similarity * 100)}%</strong><span><i style="width: ${Math.round(item.similarity * 100)}%"></i></span>`;
      row.append(copy, meter);
      list.append(row);
    });
    widget.append(header, list);
    similarGroups.append(widget);
  });
}

function setupCursorCat() {
  const cat = document.querySelector("#cursor-cat");
  if (!cat || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

  const arrivalRadius = 100;
  const catWidth = 72;
  const catHeight = 68;
  const position = { x: 28, y: Math.max(20, window.innerHeight - catHeight - 28) };
  const target = { x: window.innerWidth / 2, y: window.innerHeight / 2 };
  let lastTime = performance.now();
  let arrivedAt = lastTime;
  let state = "sleeping";

  const setTarget = (event) => {
    target.x = event.clientX;
    target.y = event.clientY;
  };

  window.addEventListener("pointermove", setTarget, { passive: true });
  window.addEventListener("pointerdown", setTarget, { passive: true });

  const animate = (time) => {
    const elapsed = Math.min((time - lastTime) / 1000, .05);
    lastTime = time;
    const centerX = position.x + catWidth / 2;
    const centerY = position.y + catHeight / 2;
    const dx = target.x - centerX;
    const dy = target.y - centerY;
    const distance = Math.hypot(dx, dy);

    if (distance > arrivalRadius) {
      if (state !== "walking") {
        state = "walking";
        cat.className = "cursor-cat is-walking";
      }
      const speed = Math.min(360, 115 + distance * .38);
      position.x += (dx / distance) * speed * elapsed;
      position.y += (dy / distance) * speed * elapsed;
      if (Math.abs(dx) > 2) cat.style.setProperty("--cat-facing", dx < 0 ? -1 : 1);
    } else {
      if (state === "walking") {
        state = "sitting";
        arrivedAt = time;
        cat.className = "cursor-cat is-sitting";
      } else if (state === "sitting" && time - arrivedAt > 900) {
        state = "sleeping";
        cat.className = "cursor-cat is-sleeping";
      }
    }

    position.x = Math.max(0, Math.min(window.innerWidth - catWidth, position.x));
    position.y = Math.max(0, Math.min(window.innerHeight - catHeight, position.y));
    cat.style.transform = `translate3d(${position.x}px, ${position.y}px, 0)`;
    requestAnimationFrame(animate);
  };

  requestAnimationFrame(animate);
}

function setupFireballs() {
  const layer = document.querySelector("#fireball-layer");
  if (!layer) return;

  let audioContext;
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

  const playExplosion = () => {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    audioContext ||= new AudioContext();
    if (audioContext.state === "suspended") audioContext.resume();

    const now = audioContext.currentTime;
    const duration = .42;
    const master = audioContext.createGain();
    master.gain.setValueAtTime(.32, now);
    master.gain.exponentialRampToValueAtTime(.001, now + duration);
    master.connect(audioContext.destination);

    const oscillator = audioContext.createOscillator();
    const oscillatorGain = audioContext.createGain();
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(105, now);
    oscillator.frequency.exponentialRampToValueAtTime(38, now + duration);
    oscillatorGain.gain.setValueAtTime(.8, now);
    oscillatorGain.gain.exponentialRampToValueAtTime(.001, now + duration);
    oscillator.connect(oscillatorGain).connect(master);
    oscillator.start(now);
    oscillator.stop(now + duration);

    const frameCount = Math.floor(audioContext.sampleRate * duration);
    const buffer = audioContext.createBuffer(1, frameCount, audioContext.sampleRate);
    const samples = buffer.getChannelData(0);
    for (let index = 0; index < frameCount; index += 1) {
      samples[index] = (Math.random() * 2 - 1) * (1 - index / frameCount);
    }
    const noise = audioContext.createBufferSource();
    const noiseFilter = audioContext.createBiquadFilter();
    noise.buffer = buffer;
    noiseFilter.type = "lowpass";
    noiseFilter.frequency.setValueAtTime(1300, now);
    noiseFilter.frequency.exponentialRampToValueAtTime(120, now + duration);
    noise.connect(noiseFilter).connect(master);
    noise.start(now);
  };

  const explode = (x, y) => {
    playExplosion();
    const impact = document.createElement("div");
    impact.className = "impact";
    impact.style.left = `${x}px`;
    impact.style.top = `${y}px`;
    impact.innerHTML = '<span class="impact-flash"></span><span class="impact-ring"></span><span class="impact-ring"></span><span class="impact-boom">BOOM!</span>';
    for (let index = 0; index < 10; index += 1) {
      const spark = document.createElement("span");
      spark.className = "impact-spark";
      spark.style.setProperty("--spark-angle", `${index * 36 + Math.random() * 16 - 8}deg`);
      spark.style.setProperty("--spark-distance", `${42 + Math.random() * 48}px`);
      impact.append(spark);
    }
    layer.append(impact);
    window.setTimeout(() => impact.remove(), 900);
  };

  window.addEventListener("pointerdown", (event) => {
    if (!event.isPrimary || event.button !== 0) return;
    const x = event.clientX;
    const y = event.clientY;
    if (reducedMotion.matches) {
      explode(x, y);
      return;
    }

    const startX = Math.max(-80, Math.min(window.innerWidth + 80, x + (Math.random() - .5) * 520));
    const startY = -90;
    const dx = x - startX;
    const dy = y - startY;
    const meteor = document.createElement("span");
    meteor.className = "meteor";
    meteor.style.left = `${startX - 11}px`;
    meteor.style.top = `${startY - 11}px`;
    meteor.style.setProperty("--meteor-angle", `${Math.atan2(dy, dx) * 180 / Math.PI}deg`);
    meteor.style.setProperty("--meteor-tail", `${Math.min(190, Math.max(105, Math.hypot(dx, dy) * .23))}px`);
    layer.append(meteor);

    const duration = Math.min(850, Math.max(430, Math.hypot(dx, dy) * .72));
    const flight = meteor.animate([
      { transform: "translate3d(0, 0, 0) scale(.72)", opacity: 0 },
      { transform: `translate3d(${dx * .08}px, ${dy * .08}px, 0) scale(1)`, opacity: 1, offset: .12 },
      { transform: `translate3d(${dx}px, ${dy}px, 0) scale(1.08)`, opacity: 1 }
    ], { duration, easing: "cubic-bezier(.34,.05,.78,.38)", fill: "forwards" });

    flight.finished.then(() => {
      meteor.remove();
      explode(x, y);
    }).catch(() => meteor.remove());
  }, { passive: true });
}
