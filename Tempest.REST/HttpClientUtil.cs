namespace Tempest.REST;

using System;
using System.Net.Http;

public class HttpClientUtil
{
    HttpClientUtil() { }

    private static readonly object Padlock = new object();
    private static HttpClientUtil? _instance = null;

    public static HttpClientUtil Instance
    {
        get
        {
            lock (Padlock)
            {
                return _instance ??= new HttpClientUtil();
            }
        }
    }

    private HttpClient? _httpClient = null;
    public HttpClient HttpClient
    {
        get { return _httpClient ??= new HttpClient { }; }
    }
}