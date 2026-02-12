package hu.zoltanterek.worldsim.refinery.controller;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Set;

import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.networknt.schema.JsonSchema;
import com.networknt.schema.JsonSchemaFactory;
import com.networknt.schema.SpecVersion;
import com.networknt.schema.ValidationMessage;

@SpringBootTest
@AutoConfigureMockMvc
class ContractSchemaTest {
    private static final ObjectMapper OBJECT_MAPPER = new ObjectMapper();
    private static JsonSchema patchRequestSchema;
    private static JsonSchema patchResponseSchema;
    private static JsonSchema errorResponseSchema;

    @Autowired
    private MockMvc mockMvc;

    @BeforeAll
    static void setUpSchemas() throws Exception {
        JsonSchemaFactory factory = JsonSchemaFactory.getInstance(SpecVersion.VersionFlag.V202012);
        patchRequestSchema = factory.getSchema(readJson("examples/schema/patch-request-v1.schema.json"));
        patchResponseSchema = factory.getSchema(readJson("examples/schema/patch-response-v1.schema.json"));
        errorResponseSchema = factory.getSchema(readJson("examples/schema/error-response.schema.json"));
    }

    @Test
    void validPatchRequestFixturesMatchSchema() throws Exception {
        assertSchemaValid(patchRequestSchema, readJson("examples/requests/patch-tech-tree-v1.json"));
        assertSchemaValid(patchRequestSchema, readJson("examples/requests/patch-world-event-v1.json"));
        assertSchemaValid(patchRequestSchema, readJson("examples/requests/patch-npc-policy-v1.json"));
    }

    @Test
    void expectedPatchResponseFixturesMatchSchema() throws Exception {
        assertSchemaValid(patchResponseSchema, readJson("examples/responses/patch-tech-tree-v1.expected.json"));
        assertSchemaValid(patchResponseSchema, readJson("examples/responses/patch-world-event-v1.expected.json"));
        assertSchemaValid(patchResponseSchema, readJson("examples/responses/patch-npc-policy-v1.expected.json"));
    }

    @Test
    void negativeRequestFixturesFailPatchRequestSchema() throws Exception {
        assertSchemaInvalid(patchRequestSchema, readJson("examples/negative/requests/patch-bad-schema.json"));
        assertSchemaInvalid(patchRequestSchema, readJson("examples/negative/requests/patch-missing-required.json"));
        assertSchemaInvalid(patchRequestSchema, readJson("examples/negative/requests/patch-bad-goal.json"));
    }

    @Test
    void livePatchResponsesMatchResponseSchema() throws Exception {
        assertLivePatchResponseSchema("examples/requests/patch-tech-tree-v1.json");
        assertLivePatchResponseSchema("examples/requests/patch-world-event-v1.json");
        assertLivePatchResponseSchema("examples/requests/patch-npc-policy-v1.json");
    }

    @Test
    void liveErrorResponsesMatchErrorSchema() throws Exception {
        assertLiveErrorResponseSchema("examples/negative/requests/patch-bad-schema.json");
        assertLiveErrorResponseSchema("examples/negative/requests/patch-missing-required.json");
        assertLiveErrorResponseSchema("examples/negative/requests/patch-bad-goal.json");
    }

    private void assertLivePatchResponseSchema(String requestFixturePath) throws Exception {
        String requestBody = Files.readString(Path.of(requestFixturePath), StandardCharsets.UTF_8);
        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isOk())
                .andReturn();
        assertSchemaValid(patchResponseSchema, parseBody(result));
    }

    private void assertLiveErrorResponseSchema(String requestFixturePath) throws Exception {
        String requestBody = Files.readString(Path.of(requestFixturePath), StandardCharsets.UTF_8);
        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isBadRequest())
                .andReturn();
        assertSchemaValid(errorResponseSchema, parseBody(result));
    }

    private static JsonNode parseBody(MvcResult result) throws Exception {
        return OBJECT_MAPPER.readTree(result.getResponse().getContentAsString());
    }

    private static JsonNode readJson(String path) throws Exception {
        return OBJECT_MAPPER.readTree(Files.readString(Path.of(path), StandardCharsets.UTF_8));
    }

    private static void assertSchemaValid(JsonSchema schema, JsonNode json) {
        Set<ValidationMessage> errors = schema.validate(json);
        assertTrue(errors.isEmpty(), () -> "Expected schema-valid JSON, got errors: " + errors);
    }

    private static void assertSchemaInvalid(JsonSchema schema, JsonNode json) {
        Set<ValidationMessage> errors = schema.validate(json);
        assertFalse(errors.isEmpty(), "Expected schema-invalid JSON, but validation succeeded");
    }
}
