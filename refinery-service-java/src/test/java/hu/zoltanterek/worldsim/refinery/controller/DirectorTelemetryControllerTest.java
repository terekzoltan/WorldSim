package hu.zoltanterek.worldsim.refinery.controller;

import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.nio.charset.StandardCharsets;
import java.util.UUID;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

@SpringBootTest(properties = {
        "planner.mode=pipeline",
        "planner.llm.enabled=false",
        "planner.refinery.enabled=true",
        "planner.director.maxRetries=0",
        "planner.director.outputMode=both"
})
@AutoConfigureMockMvc
class DirectorTelemetryControllerTest {
    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void telemetryEndpointReturnsQueryableCounters() throws Exception {
        JsonNode body = getTelemetry();
        assertTrue(body.has("directorRequestsCount"));
        assertTrue(body.has("validatedOutputsCount"));
        assertTrue(body.has("fallbackCount"));
        assertTrue(body.has("rejectedCommandCount"));
        assertTrue(body.has("retryAttemptsTotal"));
        assertTrue(body.has("averageRetryCount"));
        assertTrue(body.has("validationRetryRoundsTotal"));
        assertTrue(body.has("averageValidationRetryRounds"));
        assertTrue(body.has("llmCompletionCountTotal"));
        assertTrue(body.has("averageLlmCompletionCount"));
        assertTrue(body.has("sanitizedProposalCount"));
        assertTrue(body.has("lastUpdatedUtc"));
        assertTrue(body.has("pipelineVersion"));
    }

    @Test
    void telemetryCountersTrackDirectorRequestsAndValidation() throws Exception {
        JsonNode before = getTelemetry();

        postDirectorRequest(0, 2);
        postDirectorRequest(6, 2);

        JsonNode after = getTelemetry();

        long requestDelta = after.path("directorRequestsCount").asLong() - before.path("directorRequestsCount").asLong();
        long validatedDelta = after.path("validatedOutputsCount").asLong() - before.path("validatedOutputsCount").asLong();
        long fallbackDelta = after.path("fallbackCount").asLong() - before.path("fallbackCount").asLong();
        long rejectedDelta = after.path("rejectedCommandCount").asLong() - before.path("rejectedCommandCount").asLong();
        long completionDelta = after.path("llmCompletionCountTotal").asLong() - before.path("llmCompletionCountTotal").asLong();

        assertTrue(requestDelta >= 2);
        assertTrue(validatedDelta >= 1);
        assertTrue(fallbackDelta >= 0);
        assertTrue(rejectedDelta >= 0);
        assertTrue(completionDelta >= 0);
        assertTrue(after.path("averageRetryCount").asDouble() >= 0.0);
        assertTrue(after.path("averageValidationRetryRounds").asDouble() >= 0.0);
        assertTrue(after.path("averageLlmCompletionCount").asDouble() >= 0.0);
    }

    private JsonNode getTelemetry() throws Exception {
        MvcResult result = mockMvc.perform(get("/v1/director/telemetry"))
                .andExpect(status().isOk())
                .andReturn();
        return objectMapper.readTree(result.getResponse().getContentAsString(StandardCharsets.UTF_8));
    }

    private void postDirectorRequest(long storyBeatCooldownTicks, int colonyCount) throws Exception {
        String request = """
                {
                  "schemaVersion": "v1",
                  "requestId": "%s",
                  "seed": 321,
                  "tick": 128,
                  "goal": "SEASON_DIRECTOR_CHECKPOINT",
                  "snapshot": {
                    "world": {
                      "colonyCount": %d,
                      "storyBeatCooldownTicks": %d
                    }
                  }
                }
                """.formatted(UUID.randomUUID(), colonyCount, storyBeatCooldownTicks);

        mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(request))
                .andExpect(status().isOk());
    }
}
