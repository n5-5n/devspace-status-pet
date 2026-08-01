# Versioning policy

DevSpace Status Pet uses a patch-first version sequence during the `0.x` development period.

## Rules

1. Each independent update increments the patch number by one.
   - `v0.1.1` → `v0.1.2` → `v0.1.3`
2. Test builds for the same update keep the same base version and increment only the prerelease suffix.
   - `v0.1.3-alpha.1` → `v0.1.3-alpha.2` → `v0.1.3-alpha.3`
3. The stable release removes the prerelease suffix from the same base version.
   - `v0.1.3-alpha.3` → `v0.1.3`
4. Minor or major version changes are made only when the project deliberately adopts a broader compatibility or product milestone.

## Normalized historical releases

| Original tag | Normalized tag |
|---|---|
| `v0.1.0` | `v0.1.0` |
| `v0.2.0-alpha.1`–`alpha.6` | `v0.1.1-alpha.1`–`alpha.6` |
| `v0.2.0` | `v0.1.1` |
| `v0.2.1` | `v0.1.2` |
| `v0.3.0-alpha.1`–`alpha.3` | `v0.1.3-alpha.1`–`alpha.3` |

The normalized releases preserve the original source revisions. Release assets are rebuilt with matching executable metadata, ZIP names, and SHA-256 files.
