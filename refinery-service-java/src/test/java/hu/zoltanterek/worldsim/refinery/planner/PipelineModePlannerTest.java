package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
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

@SpringBootTest(properties = {
        "planner.mode=pipeline",
        "planner.llm.enabled=false"
})
@AutoConfigureMockMvc
class PipelineModePlannerTest {

    @Autowired
    private PatchPlanner patchPlanner;

    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void pipelineModeUsesComposedPatchPlannerBean() {
        assertInstanceOf(ComposedPatchPlanner.class, patchPlanner);
    }

    @Test
    void pipelineModeWithLlmDisabledFallsBackDeterministically() throws Exception {
        String requestBody = Files.readString(
                Path.of("examples/requests/patch-world-event-v1.json"),
                StandardCharsets.UTF_8
        );

        JsonNode first = callPatch(requestBody);
        JsonNode second = callPatch(requestBody);
        JsonNode mockExpected = readJson("examples/responses/patch-world-event-v1.expected.json");

        assertEquals(first, second);
        assertEquals(mockExpected.path("patch"), first.path("patch"));
        assertEquals("refineryStage:disabled", first.path("explain").get(0).asText());
        assertEquals(
                "LLM disabled or not implemented; using mock planner output.",
                first.path("warnings").get(0).asText()
        );
    }

    private JsonNode callPatch(String requestBody) throws Exception {
        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isOk())
                .andReturn();
        return objectMapper.readTree(result.getResponse().getContentAsString());
    }

    private JsonNode readJson(String path) throws Exception {
        return objectMapper.readTree(Files.readString(Path.of(path), StandardCharsets.UTF_8));
    }
}
