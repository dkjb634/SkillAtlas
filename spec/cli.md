### Goal

The goal of this CLI application is to scan a given repository for AI Skills and output the discovered skills in a structured format.

### Architecture

The URL to a repository is passed as an argument.

There should be console 'GUI', meaning: initially the skills are listed as "\[{NumberInTheList}\] {RepositoryName}:{Name}\n{ShortDescription}". On the right side next to the node there should be a 'widget' holding the file name where the skill is described and 'url' symbol (so that it is clear that clicking it will open the browser). Clicking this widget should open the url to this file in browser.

It should allow clicking the number in the list to expand the more detailed skill description

The clicked node should be highlighted with blue borders. Expanding another node should collapse the previously expanded node and expand another one.

The scanned data should first go to the sqlite database. It must not add duplicate entries there. So, once scanned, the data needs to be inserted. The duplicates of the same skill can exist in case the commit hash is different. Match the skill name and commit hash before inserting a new entry of the same skill.

### Tests
Use the https://github.com/JetBrains/kotlin as a sample argument.