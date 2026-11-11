# notion-body-cache Specification

## Purpose
Defines how cached Notion page bodies are bound to the renderer contract and to the page's edit time, so a renderer change or a same-minute edit is fetched and rendered again.

## Requirements

### Requirement: Notion body cache is bound to the renderer contract

The Notion page body cache MUST store the Notion renderer contract version and the time the body was fetched, taken as the earliest Notion response `Date` header among the requests that produced the body, and only when no such header is available from the local UTC clock. In readwrite mode, a cached body MUST be reused only when the entry uses the current cache format, its renderer contract version equals the current one, its `last_edited_time` is non-empty and matches, its signed links have not expired, and it was fetched at or after five seconds past the end of the minute containing `last_edited_time`. In readonly mode, a cached body in any cache format this engine can parse MUST be reused whenever its `last_edited_time` matches, regardless of renderer contract version or fetch time, and Bukit MUST emit one summary warning counting entries whose renderer contract is missing or different.

#### Scenario: Renderer contract bumped
- **WHEN** the renderer contract version changes and a page has not been edited
- **THEN** a readwrite build fetches and renders the page body again and rewrites the cache entry

#### Scenario: Edited within the fetch minute
- **WHEN** a body was fetched at 10:00:30 and `last_edited_time` is still 10:00
- **THEN** the next readwrite build fetches the body again instead of reusing the cache

#### Scenario: Settled cache
- **WHEN** the renderer contract, `last_edited_time` and link expiry are unchanged and the body for a 10:00 edit was fetched at or after 10:01:05
- **THEN** the cached body is reused without a Notion request

#### Scenario: Local clock ahead of Notion
- **WHEN** the local clock runs 30 seconds ahead of Notion, a page is edited at server time 10:00:50 and its body is fetched at server time 10:00:55
- **THEN** the cache entry records 10:00:55 and the next readwrite build fetches the body again

#### Scenario: Missing last edited time
- **WHEN** a cache entry has no `last_edited_time`
- **THEN** a readwrite build does not reuse it

#### Scenario: Readonly cache after an upgrade
- **WHEN** readonly mode loads entries written by an earlier renderer contract or cache format
- **THEN** the cached bodies are used without Notion requests and one summary warning reports the mismatch

### Requirement: Renderer contract changes are enforced by a corpus test

A fixed Notion block corpus MUST cover every block type registered in `NotionBlockRendererRegistry`, list-item grouping, rich-text annotations and the unknown-block fallback. A renderer contract test MUST keep an append-only record of corpus output hashes per renderer contract version and MUST fail when the output changes while the contract version stays the same.

#### Scenario: Renderer output changed without a version bump
- **WHEN** the corpus renders different HTML and the contract version is unchanged
- **THEN** the renderer contract test fails and names the contract version to bump

#### Scenario: New block type without corpus coverage
- **WHEN** a block renderer is registered whose type is absent from the corpus
- **THEN** the renderer contract test fails
