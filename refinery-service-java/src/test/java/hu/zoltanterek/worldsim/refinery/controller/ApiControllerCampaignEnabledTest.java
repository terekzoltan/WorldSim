package hu.zoltanterek.worldsim.refinery.controller;

import static org.junit.jupiter.api.Assertions.assertEquals;
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
        "planner.director.campaignEnabled=true"
})
@AutoConfigureMockMvc
class ApiControllerCampaignEnabledTest {
    @Autowired
    private MockMvc mockMvc;

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void patchSeasonDirectorCampaignEnabledFixtureMatchesExpectedResponse() throws Exception {
        JsonNode actual = postFixture("examples/requests/patch-season-director-campaign-enabled-v1.json");
        JsonNode expected = readJson("examples/responses/patch-season-director-campaign-enabled-v1.expected.json");
        assertEquals(expected, actual);
    }

    private JsonNode postFixture(String fixturePath) throws Exception {
        String requestBody = Files.readString(Path.of(fixturePath), StandardCharsets.UTF_8);
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
