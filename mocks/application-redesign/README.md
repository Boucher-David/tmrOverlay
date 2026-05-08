# Application Redesign Mock

Static exploratory screenshots for the outrun visual redesign concept.

This folder is not a runtime screenshot contract. The tracked mac-harness screenshots under the other `mocks/` folders remain the validation source for current production UI behavior.

## Generated Sets

- `settings-tabs/` contains one 1240x680 concept screenshot for every current Settings UI tab: General, the 12 managed overlay tabs, and Support.
- `overlays/` contains one concept screenshot for every managed overlay at its current default overlay dimensions.
- `outrun-settings-tabs-contact-sheet.png` previews the full Settings tab set.
- `outrun-overlays-contact-sheet.png` previews the full managed overlay set.
- `outrun-settings-redesign.svg` and `outrun-settings-redesign.svg.png` are the original seed concept from the first pass.

Regenerate the PNG set with:

```bash
swift tools/render_outrun_redesign.swift
```
