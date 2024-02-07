# RunCleaner

Automatically cleans out workflow runs from repositories

## Usage

Envoke with:

```shell
./RunCleaner
```

### Environment variables:

**Mandatory:**
* `GITHUB_TOKEN` - Github [PAT](https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token), must have `repo` scope
* `GITHUB_OWNER` - Owner of the repositories (github.repository_owner)

Optional:
* `EXCLUDE_PROJECTS` - Array of projects you wish to exclude in escaped JSON format
* `CLEAN_OLDER_THAN` - Runs that are older than the number of days declared here will get cleaned, defaults to 7

## Building

Requirement: [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

Build with:
```shell
dotnet build
```

## Examples

Clean every runs from repositories that are older than 7 days:
```shell
GITHUB_TOKEN=<token> GITHUB_OWNER=<username> ./RunCleaner
```

Increase maximum workflow run age to 10 days:
```shell
CLEAN_OLDER_THAN=10 GITHUB_TOKEN=<token> GITHUB_OWNER=<username> ./RunCleaner
```

Exclude repositories `owner/repo1` and `owner/repo2` from cleaning:
```shell
EXCLUDE_PROJECTS=[\"owner/repo1\", \"owner/repo2\"] GITHUB_TOKEN=<token> GITHUB_OWNER=<username> ./RunCleaner
```

## License

The scripts and documentation in this project are released under the [MIT](LICENSE.md) License.

## Credits

[LinkHeaders.cs](Source/LinkHeaders.cs) - Extension of [pimbrouwers](https://gist.github.com/pimbrouwers/8f78e318ccfefff18f518a483997be29)'s