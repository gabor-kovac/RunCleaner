using System.Text.Json.Serialization;

namespace GithubApi;

record Workflow(ulong Id, [property: JsonPropertyName("created_at")] DateTime CreatedAt);
record WorkflowRuns(int totalCount, [property: JsonPropertyName("workflow_runs")] Workflow[] Items);

record Resource(int limit, int used, int remaining, long reset);
record RateLimit([property: JsonPropertyName("resources")] Dictionary<string, Resource> Resources);

record Repository([property: JsonPropertyName("full_name")] string full_name);