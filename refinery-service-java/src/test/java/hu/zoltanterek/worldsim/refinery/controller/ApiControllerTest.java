package hu.zoltanterek.worldsim.refinery.controller;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

@SpringBootTest
@AutoConfigureMockMvc
class ApiControllerTest {

    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void healthReturnsOk() throws Exception {
        MvcResult result = mockMvc.perform(get("/health"))
                .andExpect(status().isOk())
                .andReturn();

        assertJsonEquals(
                readJson("examples/responses/health.expected.json"),
                parseBody(result)
        );
    }

    @Test
    void patchTechTreeFixtureMatchesExpectedResponse() throws Exception {
        assertPatchFixture("patch-tech-tree-v1");
    }

    @Test
    void patchWorldEventFixtureMatchesExpectedResponse() throws Exception {
        assertPatchFixture("patch-world-event-v1");
    }

    @Test
    void patchNpcPolicyFixtureMatchesExpectedResponse() throws Exception {
        assertPatchFixture("patch-npc-policy-v1");
    }

    @Test
    void patchBadSchemaReturns400WithErrorFields() throws Exception {
        JsonNode body = postFixture("examples/negative/requests/patch-bad-schema.json", status().isBadRequest());
        assertEquals("Bad request", body.path("message").asText());
        assertTrue(body.path("details").isArray());
        assertTrue(body.path("details").size() > 0);
        assertTrue(body.path("details").get(0).asText().contains("schemaVersion"));
    }

    @Test
    void patchMissingRequiredReturns400WithErrorFields() throws Exception {
        JsonNode body = postFixture("examples/negative/requests/patch-missing-required.json", status().isBadRequest());
        assertEquals("Validation failed", body.path("message").asText());
        assertTrue(body.path("details").isArray());
        assertTrue(body.path("details").size() > 0);
        assertDetailsContainIgnoreCase(body, "goal");
    }

    @Test
    void patchBadGoalReturns400WithErrorFields() throws Exception {
        JsonNode body = postFixture("examples/negative/requests/patch-bad-goal.json", status().isBadRequest());
        assertEquals("Bad request", body.path("message").asText());
        assertTrue(body.path("details").isArray());
        assertTrue(body.path("details").size() > 0);
        assertDetailsContainIgnoreCase(body, "goal");
    }

    private void assertPatchFixture(String fixtureName) throws Exception {
        JsonNode actual = postFixture("examples/requests/" + fixtureName + ".json", status().isOk());
        JsonNode expected = readJson("examples/responses/" + fixtureName + ".expected.json");
        assertJsonEquals(expected, actual);
    }

    private JsonNode postFixture(String fixturePath, org.springframework.test.web.servlet.ResultMatcher status) throws Exception {
        String requestBody = Files.readString(Path.of(fixturePath), StandardCharsets.UTF_8);
        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status)
                .andReturn();
        return parseBody(result);
    }

    private JsonNode parseBody(MvcResult result) throws Exception {
        return objectMapper.readTree(result.getResponse().getContentAsString());
    }

    private JsonNode readJson(String path) throws Exception {
        return objectMapper.readTree(Files.readString(Path.of(path), StandardCharsets.UTF_8));
    }

    private void assertJsonEquals(JsonNode expected, JsonNode actual) {
        assertEquals(expected, actual, () -> "Expected JSON: " + expected + " but got: " + actual);
    }

    private void assertDetailsContainIgnoreCase(JsonNode body, String token) {
        String combined = body.path("details").toString().toLowerCase();
        assertTrue(combined.contains(token.toLowerCase()), "Expected details to contain token: " + token);
    }
}
