---
alwaysApply: true
description: >
  Testing strategy, patterns, and naming conventions for this solution.
  Stack-agnostic — no web/DB test infrastructure assumed, since this project
  has none.
---

# Testing Rules

There is no test project in this solution yet (see project root `CLAUDE.md`). These
conventions apply once one is added — most likely xUnit against `HexGrid.Core`, which is
pure geometry/labelling/SVG logic with no I/O.

## Test Structure

- **AAA pattern with clear separation.** Arrange, Act, Assert — separated by blank lines. Each section should be immediately identifiable.

```csharp
[Fact]
public void ToPolygon_PointyOrientation_ReturnsSixVertices()
{
    // Arrange
    var hex = new Hex(q: 0, r: 0);

    // Act
    var polygon = hex.ToPolygon(HexOrientation.Pointy, size: 10f);

    // Assert
    polygon.Length.Should().Be(6);
}
```

- **One assertion concept per test.** You may assert multiple properties of the same result, but do not test two separate behaviors in one test. Separate behaviors need separate tests so failures are specific.

## Naming

- **Test naming: `MethodName_Scenario_ExpectedResult`.** Clear, searchable, and self-documenting. The test name is the specification.

```
ToPolygon_ZeroSize_ReturnsDegeneratePolygon
ExportSvg_NoHexesInBounds_ProducesEmptyDocument
```

## Fixtures and Mocking

- **No mocking frameworks for things you own.** If you control the code, use a real or test implementation. Mocking your own interfaces couples tests to implementation details and makes refactoring painful. Reserve mocks for third-party boundaries you cannot control.

## Behavior Over Implementation

- **Test behavior, not implementation details.** Assert on the observable outcome (the returned geometry, the generated SVG, the exported file), not on which internal methods were called. Tests coupled to internals break on every refactor.
