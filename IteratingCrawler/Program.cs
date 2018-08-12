using System;
using System.Collections.Generic;
using System.Linq;
using Abot.Crawler;
using Abot.Poco;
using System.IO;
using CommandLine;
using Abot.Core;
using Robots;
using System.Text.RegularExpressions;

//shoutouts to http://www.vanaqua.org:80/index.php/download_file/view/ for inspiring this project;

namespace IteratingCrawler
{
    class Program
    {
        class Options
        {
            [Value(0, Required = true, HelpText = "The website to crawl.")]
            public string Url { get; set; }

            [Option('i', HelpText = "Whether or not to retry invalid entries.")]
            public bool RetryInvalid { get; set; }

            [Option('v', HelpText = "Whether or not to force retry valid entries.")]
            public bool RetryValid { get; set; }

            [Option('c', Default = 1, HelpText = "How many pages to crawl at once. (probably doesn't do anything)")]
            public int ConcurrentPages { get; set; }

            [Option('t', Default = 1, HelpText = "How long to wait before timing out.")]
            public int Timeout { get; set; }

            [Option('u', HelpText = "User Agent String to use for crawling.")]
            public string UserAgentString { get; set; }

            [Option('s', Default = 0, HelpText = "Value to start crawling at.")]
            public int Start { get; set; }

            [Option('e', Default = 9999, HelpText = "Value to stop crawling at.")]
            public int End { get; set; }

            [Option('o', HelpText = "File to output all results to.")]
            public string Output { get; set; }
        }

        static void Main(string[] args)
        {
            if (args != null & args.Length > 0)
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(ParseSucceed)
                    .WithNotParsed(ParseError);
            }
            else
            {
                Parser.Default.ParseArguments<Options>(new string[] { "--help" });
            }
        }

        #region Dumb classes to ignore Robots.txt

        private class NobotDotTxtIgnorer : IRobotsDotTextFinder
        {
            public IRobotsDotText Find(Uri rootUri) => new NobotDotTxt();
        }

        private class Nobots : IRobots
        {
            public Uri BaseUri { get; set; }

            public bool Allowed(string url) => true;

            public bool Allowed(string url, string userAgent) => Allowed(url);

            public bool Allowed(Uri uri) => true;

            public bool Allowed(Uri uri, string userAgent) => Allowed(uri);

            public int GetCrawlDelay() => 0;

            public int GetCrawlDelay(string userAgent) => GetCrawlDelay();

            public IList<string> GetSitemapUrls() => new List<string>();

            public void Load(Uri robotsUri) { }

            public void Load(Stream stream, string baseUrl) { }

            public void Load(Stream stream, Uri baseUri) { }

            public void Load(TextReader reader, Uri baseUri) { }

            public void LoadContent(string fileContent, string baseUrl) { }

            public void LoadContent(string fileContent, Uri baseUri) { }
        }

        private class NobotDotTxt : IRobotsDotText
        {
            public IRobots Robots
            {
                get
                {
                    return new Nobots();
                }
                set
                {

                }
            }

            public int GetCrawlDelay(string userAgentString) => 0;

            public bool IsUrlAllowed(string url, string userAgentString) => true;

            public bool IsUserAgentAllowed(string userAgentString) => true;
        }

        #endregion

        private static void ParseSucceed(Options options)
        {
            CrawlConfiguration crawlConfig = new CrawlConfiguration()
            {
                CrawlTimeoutSeconds = options.Timeout,
                MaxConcurrentThreads = options.ConcurrentPages,
                MaxPagesToCrawl = 0,
                MaxPagesToCrawlPerDomain = 0,
                UserAgentString = options.UserAgentString ?? new Uri(options.Url).Host + " Crawler",
            };
            
            Dictionary<int, string> fileList = new Dictionary<int, string>(Math.Abs(options.Start - options.End));
            string fileToWrite = options.Output ?? new Uri(options.Url).Host + ".txt";
            if (File.Exists(fileToWrite))
            {
                string[] preliminaryFileList = File.ReadAllLines(fileToWrite);
                for(int i = 0; i < preliminaryFileList.Length; i++)
                {
                    Match entry = Regex.Match(preliminaryFileList[i], @"^(\d+) = (.*)$");
                    if (entry.Groups[0].Success)
                        fileList.Add(int.Parse(entry.Groups[1].Value), entry.Groups[2].Value);
                }
            }

            for (int i = options.Start; i != options.End; i += (i < options.End) ? 1 : -1)
            {
                if (!fileList.ContainsKey(i) || (Uri.TryCreate(fileList[i], UriKind.Absolute, out Uri entry) ? options.RetryValid : options.RetryInvalid))
                {
                    Uri uri = new Uri(options.Url + i);
                    CrawlResult rawResult;
                    using (PoliteWebCrawler crawler = new PoliteWebCrawler(crawlConfig, null, null, null, null, null, null, null, new NobotDotTxtIgnorer()))
                        rawResult = crawler.Crawl(uri);

                    string processedResult =
                        ((!rawResult.ErrorOccurred && rawResult.CrawlContext.RootUri.AbsoluteUri != uri.AbsoluteUri)
                        ? rawResult.CrawlContext.RootUri.AbsoluteUri
                        : "invalid file");

                    //HACK
                    if (!fileList.ContainsKey(i) || fileList[i] != processedResult)
                    {
                        if (!fileList.ContainsKey(i))
                            fileList.Add(i, processedResult);
                        else if (fileList[i] != processedResult)
                            fileList[i] = processedResult;
                        File.WriteAllLines(fileToWrite, fileList.Select(x => x.Key + " = " + x.Value).ToArray()); //HACK
                    }
                    Console.WriteLine(i + " = " + fileList[i]);                    
                }
            }
        }

        private static void ParseError(IEnumerable<Error> errors)
        {
            Console.WriteLine(string.Join("\n", errors.Select(x => x.Tag.ToString()).ToArray()));
        }
    }
}
