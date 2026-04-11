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
        "planner.director.outputMode=nudge_only",
        "planner.director.campaignEnabled=true"
})
@AutoConfigureMockMvc
class PipelineDirectorCampaignOutputModeNudgeOnlyTest {
    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void seasonDirectorRespectsNudgeOnlyMode_WithCampaignEnabled() throws Exception {
        String requestBody = Files.readString(
                Path.of("examples/requests/patch-season-director-v1.json"),
                StandardCharsets.UTF_8
        );

        MvcResult result = mockMvc.perform(post("/v1/patch")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(requestBody))
                .andExpect(status().isOk())
                .andReturn();

        JsonNode body = objectMapper.readTree(result.getResponse().getContentAsString());
        assertEquals(2, body.path("patch").size());
        assertTrue(arrayContains(body.path("patch"), "setColonyDirective"));
        assertTrue(arrayContains(body.path("patch"), "declareWar") || arrayContains(body.path("patch"), "proposeTreaty"));
        assertTrue(arrayContainsExplain(body.path("explain"), "directorOutputMode:nudge_only"));
    }

    private static boolean arrayContains(JsonNode patchArray, String op) {
        for (JsonNode item : patchArray) {
            if (op.equals(item.path("op").asText())) {
                return true;
            }
        }
        return false;
    }

    private static boolean arrayContainsExplain(JsonNode array, String expected) {
        for (JsonNode item : array) {
            if (expected.equals(item.asText())) {
                return true;
            }
        }
        return false;
    }
}
