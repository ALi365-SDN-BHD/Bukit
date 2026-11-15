# cli-scaffold Specification

## Purpose

Create a buildable Markdown starter site and its local starter theme through the
Core CLI, without running git, a build, or a network request.

## Requirements

### Requirement: Core initial-site command

Bukit SHALL provide `bukit new <dir>` with optional `--title <text>` and
`--language <tag>` options. The command SHALL create a complete single-language
Markdown starter site in `<dir>` and SHALL exit 0 on success. The command SHALL
be listed by `bukit --help` and SHALL be dispatchable through the Core CLI
command registry.

#### Scenario: Default site
- **WHEN** the user runs `bukit new my-site` in a writable location
- **THEN** `my-site/site.yaml`, `my-site/content/posts/welcome.md`,
  `my-site/content/pages/about.md`, `my-site/data/features-10.md`,
  `my-site/themes/starter/theme.yaml`, and `my-site/.gitignore` exist

#### Scenario: Custom title and language
- **WHEN** the user runs `bukit new my-site --title "A & B" --language zh-CN`
- **THEN** the parsed `site.title` is `A & B` and the parsed
  `site.languages` is `[zh-CN]`
- **AND** the generated pages escape the title as `A &amp; B`

### Requirement: Generated site is buildable

The generated site SHALL pass `bukit config check`, `bukit doctor`, and
`bukit build` without manual edits.

#### Scenario: Build the generated site
- **WHEN** the user runs `bukit config check`, `bukit doctor`, and
  `bukit build` in a freshly generated site
- **THEN** each command exits successfully
- **AND** the output contains the home page, the welcome post, and the about page

### Requirement: Starter content and configuration

The generated `site.yaml` SHALL declare one language through
`site.languages`, SHALL NOT set `site.url`, and SHALL set
`content.media.onFailure: keep`. The theme SHALL be `starter` and SHALL include
the StarterTheme layouts, assets, `theme.yaml`, `LICENSE`, and a 1200×630
default social image.

#### Scenario: No placeholder site URL
- **WHEN** the generated `site.yaml` is parsed
- **THEN** `site.url` is absent
- **AND** `content.media.onFailure` is `keep`

#### Scenario: Theme assets complete
- **WHEN** the generated `themes/starter/` directory is inspected
- **THEN** it contains `theme.yaml`, `LICENSE`, `assets/style.css`,
  `assets/og-default.png`, `static/robots.txt`, and the layouts referenced by
  `theme.yaml`

### Requirement: Target directory safety

The command SHALL write only into a missing or empty directory. It SHALL
normalize a trailing directory separator before checking the target; a
filesystem root SHALL remain unchanged. It SHALL refuse a file target, a
non-empty directory, and a target that is a symbolic link or reparse point,
including a link whose destination does not exist. It SHALL NOT overwrite any
existing entry.

#### Scenario: Non-empty target
- **WHEN** `<dir>` contains a file
- **THEN** the command exits non-zero and leaves the file unchanged

#### Scenario: Broken symlink target
- **WHEN** `<dir>` is a symbolic link whose destination does not exist
- **THEN** the command exits 2 through `CommandArgumentException`
- **AND** it does not create the destination

#### Scenario: Broken symlink target with a trailing separator
- **WHEN** `<dir>` is `broken/` and `broken` is a symbolic link whose destination
  does not exist
- **THEN** the command exits 2 through `CommandArgumentException`
- **AND** it does not call `Directory.CreateDirectory` on the link

### Requirement: Input validation and HTML escaping

The command SHALL reject a title shorter than 1 character, longer than 200
characters, or containing control characters. A `--title` that is present but
blank after trimming SHALL be rejected rather than replaced. The command SHALL
reject a language tag that does not use safe alphanumeric subtags separated by
hyphens. The starter theme header SHALL NOT contain the legacy
`{{-- bukit:brand --}}` placeholder. It SHALL HTML-escape the site title, site
description, and site params in the header, footer, and index templates.

#### Scenario: Blank title option
- **WHEN** the user runs `bukit new my-site --title " "`
- **THEN** the command exits non-zero and creates no site files

#### Scenario: Invalid language
- **WHEN** the user runs `bukit new my-site --language en_US`
- **THEN** the command exits non-zero and creates no site files

#### Scenario: Escaped title and description
- **WHEN** the title is `A & B` and the description is `About & more`
- **THEN** the rendered home page contains `A &amp; B` and
  `About &amp; more`, not the raw values

### Requirement: Error classification

Input and target errors SHALL exit 2 through the existing
`CommandArgumentException` path. Missing embedded resources and IO failures
SHALL exit 1 through the global error handler. The command SHALL NOT convert an
environment failure into a user-input exit code.

#### Scenario: User input error
- **WHEN** the target directory is not empty
- **THEN** the process exits 2

#### Scenario: Missing resource or IO failure
- **WHEN** an embedded starter resource is missing or a write fails with an IO
  error
- **THEN** the process exits 1

### Requirement: No side effects

The command SHALL NOT run `git init`, create a commit, build the site, deploy,
make a network request, or read a secret. It SHALL only write the starter files.

#### Scenario: New site is not a repository
- **WHEN** `bukit new my-site` succeeds
- **THEN** `my-site/.git` does not exist
- **AND** `my-site/dist` does not exist

### Requirement: Failure is atomic

If any write fails, the command SHALL remove only the files it created and the
directories it created for this run. If the target directory existed and was
empty, the directory SHALL remain empty. If the target did not exist, the
directories created for it SHALL be removed. Files created by another process
during the write SHALL NOT be deleted.

#### Scenario: Mid-write failure
- **WHEN** a starter resource cannot be read after an earlier nested file was
  written
- **THEN** the command exits non-zero
- **AND** the earlier file and the directories created for it are removed
- **AND** no partial starter remains in the target

#### Scenario: Existing empty target
- **WHEN** the command fails and the target existed as an empty directory
- **THEN** the target directory still exists and is still empty

### Requirement: Deterministic starter output

Running the command twice with the same inputs SHALL produce the same relative
file set and the same file contents, excluding timestamps that the filesystem
adds.

#### Scenario: Two fresh directories
- **WHEN** the command runs in two empty directories with the same title and
  language
- **THEN** the sorted relative paths and SHA-256 hashes of all generated files
  are identical

### Requirement: Missing resources fail loudly

If an embedded StarterTheme or StarterSite resource is missing, the command
SHALL fail with a message naming the resource. It SHALL NOT write an empty file
or a partial theme.

#### Scenario: Missing theme resource
- **WHEN** a required embedded resource cannot be resolved
- **THEN** the command exits non-zero and names the missing resource
