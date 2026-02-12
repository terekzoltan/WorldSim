# Examples and fixtures

This folder contains stable request/response fixtures for C# integration and Java integration tests.

## Layout

- `requests/`: valid `PATCH /v1/patch` request fixtures.
- `responses/`: expected deterministic responses for request fixtures.
- `negative/requests/`: invalid request fixtures for `400` path tests.
- `schema/`: JSON Schemas for request, response, and error payloads.

## Determinism contract

- Patch planning is deterministic for a given `goal + seed + tick`.
- Each patch operation has an `opId` generated via deterministic hashing to support dedupe.
- `opId` is the first idempotency mechanism for clients.

## C# test usage

1. Load request fixture JSON.
2. Call local service endpoint.
3. Parse response JSON.
4. Compare semantically (JSON object order independent) to `responses/*.expected.json`.

For negative fixtures, assert `400` plus `ErrorResponse` fields:

- `message`
- `details[]`

## Schema files

- `schema/patch-request-v1.schema.json`
- `schema/patch-response-v1.schema.json`
- `schema/error-response.schema.json`
