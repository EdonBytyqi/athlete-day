# Spec-driven development in this repo

The specification is the source of truth. Code follows the spec, and CI checks that it does.

| File | What it is |
|---|---|
| [requirements.md](requirements.md) | User stories, domain rules and numbered acceptance criteria (AC-xx) |
| [openapi.json](openapi.json) | The HTTP contract. The running API must match it exactly. |
| [decisions.md](decisions.md) | Architecture decisions and the reasons behind them |

## The workflow for every change

```
1. Spec      Add or change ACs in requirements.md (and openapi.json for API changes). Bump the version.
2. Tests     Write tests tagged with the AC IDs. They fail: that is the point.
3. Code      Implement until the tests pass.
4. Review    Open a PR. CI must be green: tests, traceability, contract, Docker build.
```

Each step is its own commit on a feature branch, so the history shows the order.

## How CI enforces it

* **Traceability** (`tests/AthleteDay.UnitTests/SpecTraceabilityTests.cs`, `Category=Spec`):
  fails if an AC in `requirements.md` has no test, or if a test references an AC that is not in the spec.
* **Tagging tests:** C# tests use `[Trait("AC", "AC-05")]`; Angular tests put the ID in the name: `it('[AC-03] …')`.
* **Contract** (`OpenApiContractTests`, AC-29): fails if the API's OpenAPI document differs from `openapi.json`.
  After you change the contract on purpose and implement it, refresh the file with:
  ```powershell
  $env:UPDATE_OPENAPI_CONTRACT = "1"; dotnet test tests/AthleteDay.IntegrationTests --filter "FullyQualifiedName~OpenApiContract"
  ```
  Review the diff: it should contain exactly the change you specified.

## History

v1.0 was written after the first implementation, to put a baseline in place. It is labelled that way in the spec.
Every version after it follows the workflow above.
