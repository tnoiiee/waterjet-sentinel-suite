# tools/fixtures — fixture generation wiring

There is no committed fixture-generator script in `tools/`. The single .NET
fixture generator for the repository lives in the test project:

```text
tests/integration/FixtureGenerator.cs      canonical structures + synthetic formulas
tests/integration/ExampleFiles.cs          config/examples generation
tests/integration/FixtureParityTests.cs    committed-file parity + update mode
```

Owner-local commands (from the repository root):

```powershell
# verify committed fixtures match .NET output
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release

# regenerate (updates packages/contracts/fixtures/ and config/examples/)
$env:WJSS_UPDATE_FIXTURES = "1"
dotnet test tests/integration/Wjss.FixtureEmission.Tests.csproj -c Release
$env:WJSS_UPDATE_FIXTURES = $null
```

After any regeneration, the TypeScript mirror must be re-checked:

```powershell
cd packages/contracts/wjss-contracts-ts
npm ci
npm run check
```

Stage 0.3A-1 closeout (2026-10-07): the fixtures in the tree are the genuine
.NET generator output transferred from the Owner's validated Working Tree; the
parity run passed 7/7 and the verdict is recorded in the validation document
§10. The generator's in-file `fixtureStatus` marker constant still reads
PROVISIONAL — dropping it is a later Owner change to the constant.
