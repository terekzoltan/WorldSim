package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
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
        "planner.llm.enabled=false",
        "planner.refinery.enabled=false",
        "planner.director.outputMode=nudge_only"
})
@AutoConfigureMockMvc
class PipelineDirectorPlannerTest {
    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void directorPipelineRespectsNudgeOnlyMode() throws Exception {
        String requestBody = Files.readString(
                Path.of("examples/requests/patch-season-director-v1.json"),
                StandardCharsets.UTF_8
        );

        JsonNode body = callPatch(requestBody);
        assertEquals(1, body.path("patch").size());
        assertEquals("setColonyDirective", body.path("patch").get(0).path("op").asText());
        assertTrue(arrayContains(body.path("explain"), "directorStage:mock"));
        assertTrue(arrayContains(body.path("explain"), "directorOutputMode:nudge_only"));
    }

    private JsonNode callPatch(String requestBody) throws Exception {
        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isOk())
                .andReturn();
        return objectMapper.readTree(result.getResponse().getContentAsString());
    }

    private static boolean arrayContains(JsonNode array, String expected) {
        for (JsonNode item : array) {
            if (expected.equals(item.asText())) {
                return true;
            }
        }
        return false;
    }
}
