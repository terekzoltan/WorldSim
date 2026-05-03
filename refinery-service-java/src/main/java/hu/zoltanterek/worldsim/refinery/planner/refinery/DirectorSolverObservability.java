package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

public final class DirectorSolverObservability {
    private DirectorSolverObservability() {
    }

    public static Report fromSolveResult(DirectorRefinerySolveResult result) {
        if (result == null) {
            return unavailable("missing_result");
        }

        String status = switch (result.status()) {
            case SUCCESS -> "success";
            case NON_SUCCESS -> "non_success";
            case LOAD_FAILURE -> "load_failure";
        };
        String generatorResult = normalizeToken(result.generatorResult(), "none");
        List<String> coverage = coverageMarkers(result);
        List<String> unsupported = unsupportedMarkers(result.unsupportedFeaturesIgnored());
        List<String> diagnostics = diagnosticCodes(result);
        String extraction = extractionStatus(result, diagnostics);
        String path = "success".equals(status) && !coverage.contains("none")
                ? "validated_core"
                : "sidecar";

        return build(path, status, generatorResult, extraction, coverage, unsupported, diagnostics);
    }

    public static Report unavailable(String diagnosticCode) {
        List<String> diagnostics = diagnosticCode == null || diagnosticCode.isBlank()
                ? List.of()
                : List.of(stableCode(diagnosticCode));
        return build("unavailable", "not_run", "none", "not_run", List.of("none"), List.of("none"), diagnostics);
    }

    public static Report notRun() {
        return build("unwired", "not_run", "none", "not_run", List.of("none"), List.of("none"), List.of());
    }

    private static Report build(
            String path,
            String status,
            String generatorResult,
            String extraction,
            List<String> coverage,
            List<String> unsupported,
            List<String> diagnostics
    ) {
        List<String> markers = new ArrayList<>();
        markers.add("directorSolverPath:" + path);
        markers.add("directorSolverStatus:" + status);
        markers.add("directorSolverGeneratorResult:" + generatorResult);
        markers.add("directorSolverExtraction:" + extraction);
        coverage.forEach(item -> markers.add("directorSolverValidatedCoverage:" + item));
        unsupported.forEach(item -> markers.add("directorSolverUnsupported:" + item));
        diagnostics.forEach(item -> markers.add("directorSolverDiagnostic:" + item));
        return new Report(path, status, generatorResult, extraction, coverage, unsupported, diagnostics, markers);
    }

    private static List<String> coverageMarkers(DirectorRefinerySolveResult result) {
        Set<String> coverage = new LinkedHashSet<>();
        for (String diagnostic : result.diagnostics()) {
            if ("validatedCoverage:story_core".equals(diagnostic)) {
                coverage.add("story_core");
            } else if ("validatedCoverage:directive_core".equals(diagnostic)) {
                coverage.add("directive_core");
            }
        }
        if (coverage.isEmpty()) {
            coverage.add("none");
        }
        return List.copyOf(coverage);
    }

    private static List<String> unsupportedMarkers(List<String> unsupportedFeatures) {
        Set<String> unsupported = new LinkedHashSet<>();
        for (String feature : unsupportedFeatures == null ? List.<String>of() : unsupportedFeatures) {
            String normalized = normalizeUnsupportedFeature(feature);
            if (!normalized.isBlank()) {
                unsupported.add(normalized);
            }
        }
        if (unsupported.isEmpty()) {
            unsupported.add("none");
        }
        return List.copyOf(unsupported);
    }

    private static String normalizeUnsupportedFeature(String feature) {
        String normalized = normalizeToken(feature, "");
        return switch (normalized) {
            case "campaign" -> "campaign";
            case "causalchain", "causal_chain" -> "causalChain";
            default -> normalized;
        };
    }

    private static List<String> diagnosticCodes(DirectorRefinerySolveResult result) {
        Set<String> codes = new LinkedHashSet<>();
        for (String diagnostic : result.diagnostics()) {
            String code = toStableDiagnosticCode(diagnostic);
            if (code != null) {
                codes.add(code);
            }
        }
        if (result.status() == DirectorRefinerySolveStatus.NON_SUCCESS) {
            codes.add("non_success");
        } else if (result.status() == DirectorRefinerySolveStatus.LOAD_FAILURE) {
            codes.add("load_failure");
        }
        return List.copyOf(codes);
    }

    private static String extractionStatus(DirectorRefinerySolveResult result, List<String> diagnostics) {
        if (result.status() == DirectorRefinerySolveStatus.SUCCESS) {
            return result.validatedOutput() != null && result.validatedOutput().isEmpty() ? "empty" : "success";
        }
        return diagnostics.stream().anyMatch(code -> code.startsWith("multiple_true_")
                        || code.endsWith("_missing")
                        || code.contains("_not_single")
                        || code.contains("_unknown")
                        || code.contains("_mismatch"))
                ? "failed"
                : "not_run";
    }

    private static String toStableDiagnosticCode(String diagnostic) {
        if (diagnostic == null) {
            return null;
        }
        if (diagnostic.startsWith("extractFailure:")) {
            String code = diagnostic.substring("extractFailure:".length());
            int detailStart = code.indexOf(':');
            if (detailStart >= 0) {
                code = code.substring(0, detailStart);
            }
            return stableCode(code);
        }
        if (diagnostic.startsWith("solverResult:load_failure")) {
            return "load_failure";
        }
        if (diagnostic.startsWith("solverResult:non_success")) {
            return "non_success";
        }
        return null;
    }

    private static String stableCode(String raw) {
        return normalizeToken(raw, "unknown")
                .replace('-', '_')
                .replace('.', '_');
    }

    private static String normalizeToken(String raw, String fallback) {
        if (raw == null || raw.isBlank()) {
            return fallback;
        }
        return raw.trim().toLowerCase(Locale.ROOT);
    }

    public record Report(
            String path,
            String status,
            String generatorResult,
            String extraction,
            List<String> coverage,
            List<String> unsupported,
            List<String> diagnostics,
            List<String> markers
    ) {
        public Report {
            coverage = List.copyOf(coverage == null ? List.of() : coverage);
            unsupported = List.copyOf(unsupported == null ? List.of() : unsupported);
            diagnostics = List.copyOf(diagnostics == null ? List.of() : diagnostics);
            markers = List.copyOf(markers == null ? List.of() : markers);
        }
    }
}
