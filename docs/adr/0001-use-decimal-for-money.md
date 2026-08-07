# 0001 — Store money as `decimal`, never `double`

**Status:** Accepted

## Context

The service holds balances and moves money between accounts. The amounts have to be
exact — a rounding error that loses a cent per transaction is unacceptable in a wallet.

`double` and `float` are binary floating-point: values like `0.1` cannot be represented
exactly, so `0.1 + 0.2 != 0.3`. Those errors accumulate over many operations.

## Decision

Use `decimal` for every monetary value in the domain, and map it to PostgreSQL
`numeric(18,2)`. Amounts are rounded to two places on input.

## Consequences

- No representation error: decimal is base-10, so `0.1 + 0.2` is exactly `0.3`.
- The database enforces the scale, so `449.5` is stored and returned as `449.50`, and
  `SUM(Balance)` is exact.
- `decimal` is slower and larger than `double`, which is irrelevant here — correctness
  wins over arithmetic speed for money.
- Equivalent to Java's `BigDecimal`, but a built-in language type in C#.
