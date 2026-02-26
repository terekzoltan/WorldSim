package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.List;
import java.util.Map;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

public final class OpenRouterClient {
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper;
    private final String baseUrl;
    private final String apiKey;
    private final String httpReferer;
    private final String appTitle;
    private final int timeoutMs;

    public OpenRouterClient(
            ObjectMapper objectMapper,
            String baseUrl,
            String apiKey,
            String httpReferer,
            String appTitle,
            int timeoutMs
    ) {
        this.objectMapper = objectMapper;
        this.baseUrl = baseUrl;
        this.apiKey = apiKey;
        this.httpReferer = httpReferer;
        this.appTitle = appTitle;
        this.timeoutMs = timeoutMs;
        this.httpClient = HttpClient.newBuilder()
                .connectTimeout(Duration.ofMillis(Math.max(500, timeoutMs)))
                .build();
    }

    public String chatCompletion(String model, double temperature, int maxTokens, String systemPrompt, String userPrompt) throws Exception {
        Map<String, Object> body = Map.of(
                "model", model,
                "temperature", temperature,
                "max_tokens", maxTokens,
                "messages", List.of(
                        Map.of("role", "system", "content", systemPrompt),
                        Map.of("role", "user", "content", userPrompt)
                )
        );

        String requestBody = objectMapper.writeValueAsString(body);
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + "/chat/completions"))
                .timeout(Duration.ofMillis(Math.max(500, timeoutMs)))
                .header("Content-Type", "application/json")
                .header("Authorization", "Bearer " + apiKey)
                .header("HTTP-Referer", httpReferer)
                .header("X-Title", appTitle)
                .POST(HttpRequest.BodyPublishers.ofString(requestBody))
                .build();

        HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        if (response.statusCode() < 200 || response.statusCode() >= 300) {
            throw new IllegalStateException("OpenRouter request failed with status " + response.statusCode());
        }

        JsonNode root = objectMapper.readTree(response.body());
        JsonNode messageContent = root.path("choices").path(0).path("message").path("content");
        if (messageContent.isMissingNode() || messageContent.isNull()) {
            throw new IllegalStateException("OpenRouter response missing choices[0].message.content");
        }

        if (messageContent.isTextual()) {
            return messageContent.asText();
        }

        if (messageContent.isArray() && messageContent.size() > 0) {
            return messageContent.get(0).path("text").asText("");
        }

        return messageContent.toString();
    }
}
