# Apportionment Paradox

A small .NET 10 laboratory for the **Alabama paradox**: increasing the total number of seats can make a group lose a seat under Hamilton's largest-remainder method.

Compare Hamilton, Jefferson, and Webster using exact integer and rational arithmetic, sweep house sizes, search deterministic population distributions, and generate standalone SVG charts. The library and CLI have no third-party runtime dependencies.

![Hamilton, Jefferson, and Webster compared at four and five seats](docs/example.svg)

## Try it

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The repository pins SDK `10.0.402`, allowing newer patches within that feature band.

From the repository root:

```sh
dotnet build
dotnet run --project src/Apportionment.Cli -- allocate --populations 5,3,1 --seats 4
dotnet run --project src/Apportionment.Cli -- sweep --populations 5,3,1 --from 4 --to 5
dotnet run --project src/Apportionment.Cli -- search --groups 3 --max-population 5 --from 0 --to 20
dotnet run --project src/Apportionment.Cli -- visualize --populations 5,3,1 --seats 4 --output artifacts/example.svg
```

`--method` accepts `hamilton`, `jefferson`, `webster`, or `all` (the default). Run with `--help` for command examples. Open the generated SVG in a browser; it is self-contained and includes full values in hover titles.

## The smallest demonstration here

For populations **5, 3, 1**, the total population is 9:

| Group | Population | Quota at H=4 | Hamilton seats | Quota at H=5 | Hamilton seats |
|---|---:|---:|---:|---:|---:|
| G1 | 5 | 20/9 | 2 | 25/9 | 3 |
| G2 | 3 | 12/9 | 1 | 15/9 | 2 |
| G3 | 1 | 4/9 | **1** | 5/9 | **0** |

At H=4, G3 has the largest remainder and receives the one remaining seat. At H=5, G1 and G2 have the two largest remainders and receive the two remaining seats. G3 loses its seat even though its quota increases. This example has no remainder ties.

The corresponding sweep includes:

```text
PARADOX Hamilton populations=[5,3,1] H=4->5: G3: 1->0 (-1)
```

## Mathematical rules

For population `p[i]`, total population `P`, and house size `H`, the standard quota is `H * p[i] / P` for all three methods.

| Method | Allocation rule | Complexity per allocation |
|---|---|---|
| Hamilton | Assign quota floors; award remaining seats by descending `(H * p[i]) % P`. | O(n log n) |
| Jefferson | Start at zero seats; award each next seat to the largest `p[i] / (s[i] + 1)`. | O(Hn) |
| Webster | Start at zero seats; award each next seat to the largest `p[i] / (2*s[i] + 1)`. | O(Hn) |

Webster's priority is proportional to `p[i] / (s[i] + 1/2)`. Both divisor methods use their equivalent highest-averages formulation, avoiding a numerical divisor search and rounding ambiguities at ties. A linear scan keeps the implementation easy to audit at this scale.

All products, population totals, remainders, and priority cross-products use `BigInteger`. `Rational` stores reduced fractions with positive denominators. Allocation decisions never use floating point; SVG geometry uses floating point only after allocation. Decimal quota labels use exact integer rounding, with halves rounded away from zero.

**Ties favor the first group in the original input order.** This order stays fixed across house sizes. Generated tuples retain their labels and order; permutations are separate distributions, and scale-equivalent tuples are included. Ties can therefore affect which labeled group wins, but results are deterministic.

Hamilton respects the lower/upper quota bounds but can lose house monotonicity. Jefferson and Webster are house monotone under the fixed tie rule; divisor methods can violate quota bounds. These are different properties, so quota deviation is measured independently of paradox detection.

See the [Census Bureau's historical method descriptions](https://www.census.gov/topics/public-sector/congressional-apportionment/about/historical-perspective.html), [Young's discussion of fairness and paradoxes](https://www2.census.gov/about/history/historical-censuses-surveys/survey-census-history/decennial/fairness-in-apportionment-young.pdf), and the [divisor criteria in Housman's apportionment presentation](https://www2.goshen.edu/~dhousman/research/MAA_IN_2021_03_26_Handout.pdf).

## Sweeps, search, and metrics

`sweep` evaluates a fixed population vector at every house size in the **inclusive** range `--from` through `--to`. It prints seat vectors, exact deviation metrics, and any losses between adjacent sizes. A one-size range evaluates allocations and has no transitions.

`search` generates all labeled population tuples in `1..max-population`, lexicographically, with the last group changing fastest. The generator streams results without materializing the search space. For each tuple, methods run in Hamilton/Jefferson/Webster order, with house sizes ascending within each method.

```sh
dotnet run --project src/Apportionment.Cli -- search --groups 3 --max-population 12 --from 1 --to 30 --max-configurations 200000 --examples 5
```

The default configuration limit is **100,000**, shared across all methods and distributions. Reaching it can stop inside a method's sweep; the CLI explicitly reports partial results and per-method counts. It stops before computing an extra allocation. `--examples` limits only the number of printed search events (default 10); all observed events still count. Ctrl+C cancels work with exit code 130.

| Metric | Meaning |
|---|---|
| Population distributions examined | Tuples for which at least one allocation was computed; includes a partially examined final tuple. |
| Allocation configurations examined | One population tuple × one method × one house size. |
| Seats allocated | Sum of house sizes across a method's examined configurations. Every individual allocation checks seat conservation. |
| Adjacent transitions examined | Comparisons at H and H+1 within the same tuple and method. |
| Paradox transitions | Examined transitions with at least one group losing seats. Counted separately per method. |
| Losing group occurrences | Number of group losses across transitions, including repeated losses by the same group. |
| Seats lost | Total magnitude of those losses. |
| Quota deviation | Signed `allocated seats − standard quota`, reported per group by `allocate`. |
| Absolute deviation metrics | Per-allocation maximum and sum of absolute deviations; exploration reports the maximum observed value of each. |

The documented small search (`--groups 3 --max-population 5 --from 0 --to 20`) completes with **125 distributions, 7,875 allocations, and 7,500 transitions**. It finds **40 Hamilton paradox transitions**, and zero for Jefferson and Webster, using the stated tie rule. These counts describe this bounded enumeration, not a probability estimate.

## Input and rendering limits

- Populations must be nonnegative integers, with a positive total. Groups are named G1, G2, … by the CLI. The library accepts unique, nonblank custom IDs.
- A house size may be zero. No method guarantees a minimum seat per group. Zero-population groups receive no seats.
- The CLI allows up to 256 groups and 256 characters per population, and house sizes through 100,000.
- Search allows 1–8 groups, maximum population 1–1,000, configuration limits 1–1,000,000, and 0–100 printed examples. Generated populations are positive; explicit inputs may contain zeros.
- SVG compares H with H+1, supports up to 16 groups, and allows H through 99,999. Bars share one scale across all panels. Losses use red plus an explicit numeric label. Long labels are abbreviated visually; full values remain in hover titles. `--output` must end in `.svg`; an existing file at that path is overwritten.
- Searches are combinatorial, and divisor allocation is linear in the house size. The configuration budget bounds allocation counts, not elapsed time. Use small ranges initially.

Commands return 0 on success, including a clearly marked budget-limited exploration; 2 for invalid arguments or file errors; and 130 for cancellation. Output may already have been streamed when cancellation occurs. Decimal punctuation and numeric output are culture independent.

## Code and validation

`Apportionment.Core` contains immutable results, exact arithmetic, the three strategies behind `IApportionmentMethod`, deterministic generation, transition detection, and streaming experiment counters. `Apportionment.Cli` handles parsing, console presentation, and XML-based SVG rendering. Allocation methods snapshot inputs and share validation; every result checks nonnegative seats and an exact seat total.

```sh
dotnet test
dotnet test -c Release
dotnet format --verify-no-changes
dotnet publish src/Apportionment.Cli -c Release -o artifacts/publish
```

Tests cover known examples, exhaustive small allocations, Hamilton's quota bounds, divisor house monotonicity, an independent global-priority-table oracle, ties, huge populations, scaling, invalid inputs, cancellation, exploration budgets and counters, deterministic search, XML escaping, culture independence, and SVG file generation.

Generated output belongs in ignored `artifacts/`. The checked-in `docs/example.svg` is regenerated with:

```sh
dotnet run --project src/Apportionment.Cli -- visualize --populations 5,3,1 --seats 4 --output docs/example.svg
```

No GitHub Actions or workflows are included.
