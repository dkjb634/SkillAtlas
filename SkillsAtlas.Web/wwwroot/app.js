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
let skills = [];
let expandedSkill = null;
let similarSkillsAreStale = true;

const libraryTab = document.querySelector("#library-tab");
const similarTab = document.querySelector("#similar-tab");
const similarView = document.querySelector("#similar-view");
const similarGroups = document.querySelector("#similar-groups");
const similarLoading = document.querySelector("#similar-loading");
const similarError = document.querySelector("#similar-error");
const noSimilarResults = document.querySelector("#no-similar-results");

libraryTab.addEventListener("click", () => switchView("library"));
similarTab.addEventListener("click", () => switchView("similar"));

const initialLoad = loadSavedSkills();

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  setLoading(true);
  errorState.hidden = true;
  results.hidden = true;
  await initialLoad;

  try {
    const previousCount = skills.length;
    const response = await fetch("/api/skills", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ repository: repositoryInput.value.trim() })
    });
    const payload = await response.json();
    if (!response.ok) throw new Error(payload.message || "The repository could not be read.");
    renderLibrary(payload, payload.searchedRepository
      ? `Searched ${payload.searchedRepository}`
      : null);
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
    if (library.skills?.length) renderLibrary(library, "Loaded from your SQLite library");
    else document.querySelector("#database-location").textContent = `SQLite database: ${library.databasePath}`;
  } catch (error) {
    errorState.textContent = error.message || "The saved skill library could not be loaded.";
    errorState.hidden = false;
  }
}

function setLoading(isLoading) {
  scanButton.disabled = isLoading;
  buttonLabel.textContent = isLoading ? "Exploring…" : "Explore repository";
  loadingState.hidden = !isLoading;
  if (isLoading) errorState.hidden = true;
}

function renderLibrary(library, sourceLabel) {
  skills = library.skills || [];
  expandedSkill = null;
  filterInput.value = "";
  const repositoryCount = new Set(skills.map((skill) => skill.repositoryUrl)).size;
  document.querySelector("#repository-meta").textContent = sourceLabel
    ? `${sourceLabel} · ${skills.length} saved ${skills.length === 1 ? "skill" : "skills"} across ${repositoryCount} ${repositoryCount === 1 ? "repository" : "repositories"}`
    : `${skills.length} saved ${skills.length === 1 ? "skill" : "skills"} across ${repositoryCount} ${repositoryCount === 1 ? "repository" : "repositories"}`;
  document.querySelector("#database-location").textContent = `SQLite database: ${library.databasePath}`;
  document.querySelector("#skill-count").textContent = `${skills.length} ${skills.length === 1 ? "skill" : "skills"}`;
  welcome.hidden = true;
  results.hidden = false;
  renderSkills();
  results.scrollIntoView({ behavior: "smooth", block: "start" });
}

function renderSkills() {
  const filter = filterInput.value.trim().toLocaleLowerCase();
  const visibleSkills = skills.filter((skill) =>
    `${skill.name} ${skill.shortDescription} ${skill.relativeFilePath}`.toLocaleLowerCase().includes(filter)
  );

  skillList.replaceChildren();
  noFilterResults.textContent = skills.length === 0
    ? "No SKILL.md files were found in this repository."
    : "No skills match that filter.";
  noFilterResults.hidden = visibleSkills.length !== 0;

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
    const content = document.createElement("pre");
    content.className = "skill-content";
    content.textContent = skill.content || "No additional description is available for this skill.";
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
  libraryTab.classList.toggle("is-active", !showSimilar);
  similarTab.classList.toggle("is-active", showSimilar);
  libraryTab.setAttribute("aria-selected", String(!showSimilar));
  similarTab.setAttribute("aria-selected", String(showSimilar));
  document.querySelectorAll("main > section:not(#similar-view)").forEach((section) => {
    section.classList.toggle("view-hidden", showSimilar);
  });
  similarView.hidden = !showSimilar;
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
