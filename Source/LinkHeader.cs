using System.Text.RegularExpressions;

namespace GithubApi;

public class LinkHeader
{
    public string? FirstLink { get; private set; }
    public string? PrevLink { get; private set; }
    public string? NextLink { get; private set; }
    public string? LastLink { get; private set; }
    public bool HasNext { get => this.NextLink != null; }

    public static LinkHeader LinksFromHeader(HttpResponseMessage httpResponseMessage)
    {
        LinkHeader linkHeader = new();
        if (httpResponseMessage.Headers.Contains("Link"))
        {
            linkHeader = LinksFromHeader(httpResponseMessage.Headers.GetValues("Link").FirstOrDefault(""));
        }
        return linkHeader;
    }

    public static LinkHeader LinksFromHeader(string linkHeaderStr)
    {
        LinkHeader linkHeader = new();
        
        string[] linkStrings = linkHeaderStr.Split(',');

        if (linkStrings.Length != 0)
        {
            linkHeader = new LinkHeader();

            foreach (string linkString in linkStrings)
            {
                var relMatch = Regex.Match(linkString, "(?<=rel=\").+?(?=\")", RegexOptions.IgnoreCase);
                var linkMatch = Regex.Match(linkString, "(?<=<).+?(?=>)", RegexOptions.IgnoreCase);

                if (relMatch.Success && linkMatch.Success)
                {
                    string rel = relMatch.Value.ToUpper();
                    string link = linkMatch.Value;

                    switch (rel)
                    {
                        case "FIRST":
                            linkHeader.FirstLink = link;
                            break;
                        case "PREV":
                            linkHeader.PrevLink = link;
                            break;
                        case "NEXT":
                            linkHeader.NextLink = link;
                            break;
                        case "LAST":
                            linkHeader.LastLink = link;
                            break;
                    }
                }
            }
        }

        return linkHeader;
    }
}
