## Why

The GA4 loader can process the generated config command before the browser has
parsed the page title. A captured cold-load request therefore contained an
empty title even though the final document title was valid. The shared Core
transform must provide the final static title without sending a second page
view or changing consent ordering.

## What Changes

- Read the normalized primary title from the final HTML before rendering
  Analytics fragments.
- Supply that title to every Google Analytics destination through an encoded
  data attribute and one config call.
- Preserve browser-default title behavior when the final HTML has no title.
- Bump the renderer contract so incremental output is invalidated.

## Capabilities

### New Capabilities
- `analytics-rendering`: stable initial Google Analytics title collection.

### Modified Capabilities

None.

## Impact

Internal Analytics rendering, its CSP and incremental contracts, focused
Engine tests, and the Analytics user guide. No public configuration, plugin
protocol, dependency, site content, deployment, or Analytics property change.
