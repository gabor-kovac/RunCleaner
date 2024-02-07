using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using GithubApi;
using System.Text.Json;

#region Constants

const string GITHUB_API_URL = "https://api.github.com";

const int PAGE_SIZE = 100;

const int RATE_LIMIT_TIMEOUT_PADDING_SECONDS = 10;

#endregion

#region Locals

List<string> projects = [];

List<string> excludeProjects = [];

int rateCounter = 0;

HttpClient httpClient = new HttpClient
{
    DefaultRequestHeaders =
    {
        Accept = { new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json") }
    },
    BaseAddress = new Uri(GITHUB_API_URL)
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RunCleaner/1.0");

#endregion

#region Program

int CLEAN_WHEN_OLDER_THAN_DAYS = int.Parse(Environment.GetEnvironmentVariable("CLEAN_OLDER_THAN") ?? "7");
string EXCLUDE_PROJECTS_JSON = Environment.GetEnvironmentVariable("EXCLUDE_PROJECTS") ?? "";
string GITHUB_OWNER = Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "";
string GITHUB_TOKEN = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";

if(string.IsNullOrEmpty(GITHUB_OWNER))
{
    Console.WriteLine($"Error reading GITHUB_OWNER from environment!");
    Environment.Exit(1);
}

if(string.IsNullOrEmpty(GITHUB_TOKEN))
{
    Console.WriteLine($"Error reading GITHUB_TOKEN from environment!");
    Environment.Exit(2);
}
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", GITHUB_TOKEN);

if(!string.IsNullOrEmpty(EXCLUDE_PROJECTS_JSON))
{
    try
    {
        excludeProjects = JsonSerializer.Deserialize<List<string>>(EXCLUDE_PROJECTS_JSON) ?? [];
    }
    catch(Exception e)
    {
        Console.WriteLine($"Error reading excluded projects: {e.Message}");
    }
}
else
{
    Console.WriteLine("No EXCLUDE_PROJECTS found");
}

await GetProjects();

foreach (string project in projects)
{
    await GetRateLimit();
    await CheckCounter();

    Console.WriteLine($"Checking project {project}");

    List<Workflow> workflows = await GetWorkflows(project);

    await DeleteWorkflows(project, workflows);
}

Console.WriteLine("Cleaner successfully completed.");
Environment.Exit(0);

#endregion

#region Functions

static async Task StartCountdown(double durationInSeconds)
{
    for (double i = durationInSeconds; i > 0; i--)
    {
        Console.WriteLine($"{i} seconds remaining...");
        await Task.Delay(1000);
    }
}

async Task GetProjects()
{
    int repoPage = 1;
    bool hasNext = true;
    HttpResponseMessage response;
    do 
    {
        try 
        {
            response = await httpClient.GetAsync($"/orgs/{GITHUB_OWNER}/repos?per_page={PAGE_SIZE}&page={repoPage}");
            if(response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Checking project page {repoPage}...");
                List<Repository> repoList = response.Content.ReadFromJsonAsync<List<Repository>>().Result ?? [];

                repoList.ForEach(x => {
                    projects.Add(x.full_name);
                });

                if(LinkHeader.LinksFromHeader(response).HasNext)
                {
                    hasNext = true;
                    repoPage++;
                }
                else
                {
                    hasNext = false;
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"Error getting projects, {e.Message} exiting...");
            Environment.Exit(3);
        }
    } while(hasNext);

    Console.WriteLine($"Found {projects.Count} projects");

    if(excludeProjects.Count > 0){
        Console.WriteLine($"Excluding {excludeProjects.Count} projects");
        excludeProjects.ForEach(x => projects.Remove(x));
    }

    Console.WriteLine($"Cleaning {projects.Count} projects");
}

async Task<(bool limited, Resource? resource)> GetRateLimit()
{
    (bool, Resource?) result = (false, null);
    try
    {
        HttpResponseMessage response = await httpClient.GetAsync($"/rate_limit");
        RateLimit? data = await response.Content.ReadFromJsonAsync<RateLimit>();

        if(data != null && data.Resources["core"] != null)
        {
            Resource resource = data.Resources["core"];

            rateCounter = resource.remaining;
            result = (resource.remaining <= 0, resource);
        }
    }
    catch(Exception e)
    {
        Console.WriteLine($"Error getting rate limit: {e.Message}");
    }

    return result;
}

async Task CheckCounter()
{
    if(rateCounter <= 5){
        Console.WriteLine("Nearing API limit, checking remaining rate");
        var result = await GetRateLimit();
        if(result.resource != null){
            rateCounter = result.resource.remaining;

            DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(result.resource.reset);
            DateTimeOffset currentTime = DateTimeOffset.UtcNow;

            TimeSpan timeUntilReset = resetTime - currentTime;

            if(result.resource.remaining <= 5)
            {
                Console.WriteLine($"Reached Github API rate limit, {result.resource.remaining}/{result.resource.limit}, resets in {timeUntilReset.Minutes}m {timeUntilReset.Seconds}s");
                await StartCountdown(timeUntilReset.TotalSeconds + RATE_LIMIT_TIMEOUT_PADDING_SECONDS);
            }
            else
            {
                Console.WriteLine($"API rate remaining: {result.resource.remaining}/{result.resource.limit}, resets in {timeUntilReset.Minutes}m {timeUntilReset.Seconds}s");
            }
        }
        else
        {
            Console.WriteLine("Error checking rate limit");
        }
    }
}

// Get the list of workflows of a project
async Task<List<Workflow>> GetWorkflows(string project)
{
    List<Workflow> workflows = [];

    bool hasNext = true;
    int pageIndex = 1;
    do
    {
        string pageUrl = $"/repos/{project}/actions/runs?per_page={PAGE_SIZE}&page={pageIndex}";
        try 
        {
            await CheckCounter();

            Console.WriteLine($"Checking runs page {pageIndex}...");
            HttpResponseMessage result = await httpClient.GetAsync(pageUrl);
            rateCounter--;

            if(!result.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error requesting page ({pageUrl})");
                Console.WriteLine($"{(int)result.StatusCode} {result.StatusCode}");
                switch (result.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        Console.WriteLine("Is your token valid?");
                        break;
                    case HttpStatusCode.NotFound:
                        Console.WriteLine("Is your target valid?");
                        break;
                    default:
                        break;
                }
                Console.WriteLine("Skipping...");
            }
            else
            {
                if(LinkHeader.LinksFromHeader(result).HasNext)
                {
                    hasNext = true;
                    pageIndex++;
                }
                else
                {
                    hasNext = false;
                }

                WorkflowRuns? page = await result.Content.ReadFromJsonAsync<WorkflowRuns>();
                if(page != null)
                {
                    workflows.AddRange(page.Items);
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"Exception ocurred: {e.Message}, check your url ({httpClient.BaseAddress}{pageUrl.TrimStart('/')}), exiting...");
            Environment.Exit(2);
        }
        
    } while (hasNext);

    return workflows;
}

// Delete workflows older than DeleteWhenOlderThanDays days
async Task DeleteWorkflows(string project, List<Workflow> workflows)
{
    if (workflows.Count > 0)
    {
        Console.WriteLine($"Project has {workflows.Count} workflow runs in total");
        DateTime timeWindow = DateTime.UtcNow.AddDays(-CLEAN_WHEN_OLDER_THAN_DAYS);
        workflows = workflows.Where(x => x.CreatedAt < timeWindow).ToList();
        Console.WriteLine($"{workflows.Count} workflow runs are older than {CLEAN_WHEN_OLDER_THAN_DAYS} days");
    }
    else Console.WriteLine("Project has been already cleaned, skipping...");

    foreach (ulong runId in workflows.Select(x => x.Id))
    {
        string deleteUrl = $"/repos/{project}/actions/runs/{runId}";
        try
        {
            await CheckCounter();

            Console.Write($"Deleting workflow run {runId}...");
            HttpResponseMessage response = await httpClient.DeleteAsync(deleteUrl);
            rateCounter--;

            if(response.IsSuccessStatusCode)
            {
                Console.Write(" deleted\n");
            }
            else
            {
                Console.Write(" failure\n");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deleting workflow run {runId}: {e.Message}");
        }       
    }
}

#endregion