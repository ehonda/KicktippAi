# OpenAI Flex-Then-Standard Fallback

## Decision

Prediction requests use the OpenAI Responses API with a one-step application-level fallback:

1. Send the first request with `service_tier` set to `flex`.
2. If that request fails with a capacity-style flex failure, retry once without `service_tier`.
3. Treat the second request as standard/default processing and record that fallback in Langfuse metadata.

The OpenAI SDK retry policy stays disabled with `ClientRetryPolicy(maxRetries: 0)`. The .NET HTTP resilience pipeline owns retry and fallback behavior.

## Why Flex First

This workflow is a cost optimization. Flex processing is cheaper when OpenAI has flex capacity available, so the normal path tries flex first.

We could do more flex retries before falling back to standard processing. That might save a little more money during short-lived flex capacity gaps, but it would also add latency, complicate the retry policy, and make failure behavior harder to reason about. One flex attempt is simple and should capture most of the savings when flex capacity is healthy.

## Fallback Classification

The fallback is intentionally narrow. We retry without `service_tier` for failures that look like flex capacity exhaustion or request timeout:

- HTTP 408.
- HTTP 429 only when the response looks resource/capacity related, such as `resource_unavailable`, insufficient resources, or capacity wording.
- Timeout exceptions where the caller did not cancel the request.

An ordinary rate limit 429 is not retried as standard processing. That keeps quota and traffic-shaping behavior visible instead of silently turning it into a different request.

## Why Not SDK Retries

SDK retries are not suitable for this workflow because they retry the same request. The key fallback behavior is changing the request from `service_tier = flex` to no `service_tier`, which the SDK retry policy cannot express.

The SDK retry policy also does not encode our capacity-specific classification for 429 responses. We need to distinguish flex resource unavailability from ordinary rate limiting because only the former should become a standard-tier retry.

Finally, SDK retries would blur fallback telemetry. The application-level pipeline records the requested tier, final tier, fallback strategy, and whether fallback was used in Langfuse metadata. Keeping this logic in one place makes cost attribution and operational debugging much clearer.
