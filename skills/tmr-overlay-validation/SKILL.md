---
name: tmr-overlay-validation
description: Use when validating tmrOverlay changes, especially before handing code back from a non-Windows machine or when .NET build/test cannot run locally. Includes repository-specific static checks and the Windows build/test gate.
---

# TmrOverlay Validation

Use this skill before finalizing code changes in this repo.

## Quick Checks

1. Run the duplicate C# member scanner:

   ```bash
   python3 skills/tmr-overlay-validation/scripts/check-csharp-member-duplicates.py
   ```

   This scans `src/` and `tests/` for duplicate member names inside C# types. It specifically catches record primary-constructor generated properties colliding with methods/properties/fields declared in the type body, such as a positional record property named `SkiesLabel` and a helper method also named `SkiesLabel`.

2. When `dotnet` is available, run the Windows production gate:

   ```powershell
   dotnet test .\tmrOverlay.sln
   ```

3. If `dotnet` is not available locally, state that clearly and rely on the static checks plus targeted source inspection. Windows build/test verification still needs to run on a .NET-equipped machine.

## When Touching Snapshots Or Read Models

- Run the duplicate scanner after adding positional record parameters or helper methods.
- Prefer helper method names that describe the operation, such as `FormatSkiesLabel` or `DetermineDeclaredWetSurfaceMismatch`, rather than names identical to generated record properties.
- Keep public snapshot member names stable unless the requested change intentionally updates the contract.
