using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace GitHubApiTests
{
    class Program
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
        private const string GitHubApiUrl = "https://api.github.com/";

        static void Main(string[] args)
        {
            CreateRelease(
                Environment.GetEnvironmentVariable("appveyor_repo_name"),
                Environment.GetEnvironmentVariable("appveyor_build_version"),
                Environment.GetEnvironmentVariable("appveyor_build_version"),
                Environment.GetEnvironmentVariable("appveyor_repo_commit"),
                false,
                false,
                null
                );
        }

        private static async void CreateRelease(string repositoryName, string tagName, string releaseName, string commitId, bool draftRelease, bool preRelease, string description)
        {
            tagName = tagName.Replace(" ", "-");

            Console.WriteLine(String.Format("Creating \"{0}\" release for repository \"{1}\" tag \"{2}\" commit \"{3}\"...", releaseName, repositoryName, tagName, commitId));

            // check if release already exists
            JToken release = null;
            using (var client = GetClient())
            {
                var response = await client.GetAsync("repos/" + repositoryName + "/releases?page=1&per_page=10");
                if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("Invalid authentication token or repository not found.");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(String.Format("Error reading repository '{0}' releases: {1} - {2}", repositoryName, (int)response.StatusCode, response.ReasonPhrase));
                }
                var releases = await response.Content.ReadAsAsync<JToken[]>();
                release = releases.SingleOrDefault(r => r.Value<string>("tag_name") == tagName);
            }

            string body = description;
            if (!String.IsNullOrEmpty(body))
            {
                // replace \n with real LF
                body = body.Replace("\\n", "\n");
            }

            // create new release
            if (release == null)
            {
                using (var client = GetClient())
                {
                    var response = await client.PostAsJsonAsync("repos/" + repositoryName + "/releases", new
                    {
                        tag_name = tagName,
                        target_commitish = commitId,
                        name = releaseName,
                        draft = draftRelease,
                        prerelease = preRelease,
                        body = body
                    });
                    response.EnsureSuccessStatusCode();
                    release = await response.Content.ReadAsAsync<JToken>();
                }

                Console.WriteLine("OK");
            }
            else
            {
                Console.Write("Skipped");
                Console.WriteLine(String.Format(" (Release with tag \"{0}\" already exists)", tagName));
            }

            Console.WriteLine("Upload URL: " + release.Value<string>("upload_url"));
        }

        private static HttpClient GetClient()
        {
            var authToken = Environment.GetEnvironmentVariable("gh_token");

            var client = new HttpClient();
            client.BaseAddress = new Uri(GitHubApiUrl);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppVeyor", "3.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", authToken);
            client.Timeout = RequestTimeout;

            return client;
        }
    }
}
