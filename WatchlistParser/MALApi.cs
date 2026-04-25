using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography;
using Newtonsoft.Json;
using P90Ez.MALApi.Structures;

namespace P90Ez.MALApi
{
	class MALAppAuth
	{
		protected readonly string BaseApiURI = "https://api.myanimelist.net/v2/";
		protected readonly string Fields_AllDetails = "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,rank,popularity,num_list_users,num_scoring_users,nsfw,created_at,updated_at,media_type,status,genres,my_list_status,num_episodes,start_season,broadcast,source,average_episode_duration,rating,pictures,background,related_anime,related_manga,recommendations,studios,statistics";
		protected string ClientId { get; }
		protected bool EnableNSFWContent { get; }

		/// <summary>
        /// Creates a My Anime List API client using application authentication. Not bound to a specific user.
        /// </summary>
        /// <param name="ClientId">Id of your MAL registered client.</param>
        /// <param name="EnableNSFWContent">Excludes or includes "not safe for work" rated anime and manga.</param>
		public MALAppAuth(string ClientId, bool EnableNSFWContent = false)
		{
			this.ClientId = ClientId;
			this.EnableNSFWContent = EnableNSFWContent;
		}

		/// <summary>
		/// Creates the http-client's authentication header.
		/// </summary>
		protected virtual AuthenticationHeaderValue GetAuthHeader()
		{
			return new AuthenticationHeaderValue("X-MAL-CLIENT-ID", ClientId);
		}
		
		/// <summary>
        /// Handles requests to the API - includes retries on error and response parsing.
        /// </summary>
        /// <typeparam name="T">Expected response type.</typeparam>
        /// <param name="Request">Request information.</param>
        /// <param name="Retries">Number of retries before abort.</param>
        /// <returns>Null on error. Object of expected response type on success.</returns>
		protected virtual T? MultiTryApiCom<T>(HttpRequestMessage Request, int Retries = 3)
        {
			int Try = 0;
			bool Success = false;

			while (!Success && Try < Retries)
			{
				Success = true;

				try
				{
					//make request
					HttpClient Client = new HttpClient();
					Client.DefaultRequestHeaders.Authorization = GetAuthHeader();

					HttpResponseMessage Response = Client.Send(Request);

					Success = Response.IsSuccessStatusCode;

					//parsing
					if (Success)
					{
						string ResponseBody = Response.Content.ReadAsStringAsync().Result;

						T? ParsedContent = JsonConvert.DeserializeObject<T>(ResponseBody);

						Success = ParsedContent != null;
						if (Success) return ParsedContent;
					}
				}
				catch
				{
					Success = false;
				}

				if (!Success) Try++;
			}

			return default;
        }

		/// <summary>
        /// Gets details to the anime with the given id.
        /// </summary>
        /// <param name="AnimeId">Id of the anime.</param>
        /// <returns>An object containing the shows details on success. Null on error.</returns>
		public AnimeDetails? GetAnimeDetails(long AnimeId)
		{
			HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Get, BaseApiURI + $"anime/{AnimeId}?fields={Fields_AllDetails}&nsfw={EnableNSFWContent.ToString().ToLower()}");
			return MultiTryApiCom<AnimeDetails>(Request);
		}
    }

	class MALUserAuth : MALAppAuth
	{
		private AccessTokenHandler TokenHandler { get; }
		protected string AccessToken => TokenHandler.GetAccessToken();

		/// <summary>
        /// Creates a My Anime List API client using application authentication. Bound to a specific user. Can retrieve and edit the user's lists.
        /// </summary>
        /// <param name="TokenHandler">Token handler.</param>
        /// <param name="EnableNSFWContent">Excludes or includes "not safe for work" rated anime and manga.</param>
		public MALUserAuth(AccessTokenHandler TokenHandler, bool EnableNSFWContent = false) : base(TokenHandler.ClientId, EnableNSFWContent)
		{
			this.TokenHandler = TokenHandler;
		}

		protected override AuthenticationHeaderValue GetAuthHeader()
		{
			return new AuthenticationHeaderValue("Bearer", AccessToken);
		}
		
		/// <summary>
        /// Retrieves the user's anime watchlist.
        /// </summary>
        /// <returns>A list containing details to every show on the user's anime watchlist. An empty list on error.</returns>
		public List<AnimeDetails> GetUserWatchlist()
		{
			int PagingLimit = 100;
			string RequestString = BaseApiURI + $"users/@me/animelist?fields={Fields_AllDetails}&limit={PagingLimit}&nsfw={EnableNSFWContent.ToString().ToLower()}";
			List<AnimeDetails> Watchlist = new List<AnimeDetails>();

			while (RequestString != "")
			{
				HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Get, RequestString);
				DefaultAPIResponse<AnimeDetails>? DataPage = MultiTryApiCom<DefaultAPIResponse<AnimeDetails>>(Request);

				RequestString = "";
				if (DataPage != null)
				{
					foreach(var WatchlistItem in DataPage.Data)
                    {
						Watchlist.Add(WatchlistItem.Node);
                    }
					if (DataPage.Data.Count > 0) RequestString = DataPage.GetNextPageURI();
				}
			}

			return Watchlist;
        }
	}
	
	class AccessTokenHandler
	{
		protected readonly string OAuthURL = "https://myanimelist.net/v1/oauth2/";
		protected string RedirectURI { get; }
		public string ClientId { get; }
		protected string ClientSecret { get; }
		protected MalTokenResponse TokenContext { get; set; }
		private readonly object _TokenContextLock = new object();
		private string? CredentialsFilename { get; }

		/// <summary>
        /// Handles creating and refreshing My Anime List API access tokens.
        /// </summary>
        /// <param name="ClientId">Id of your MAL registered client.</param>
        /// <param name="ClientSecret">Secret of your MAL registered client.</param>
        /// <param name="RedirectURI">Your MAL registered client's redirect URI.</param>
        /// <param name="CredentialsFilename">File name or path where to store the credentials. Null if credentials should not be stored.</param>
		public AccessTokenHandler(string ClientId, string ClientSecret, string RedirectURI = "http://127.0.0.1:9876/", string? CredentialsFilename = null)
		{
			lock (_TokenContextLock)
			{
				this.ClientId = ClientId;
				this.ClientSecret = ClientSecret;
				this.RedirectURI = RedirectURI;
				this.CredentialsFilename = CredentialsFilename;
				MalTokenResponse? TokenContext = CreateAccessToken();

				if (TokenContext == null) throw new AuthenticationException("Failed to create an access token!");

				this.TokenContext = TokenContext;
			}

			SaveCredentials();
		}

		private AccessTokenHandler(CredentialsSaveStructure Credentials, string CredentialsFilename)
		{
			lock (_TokenContextLock)
			{
				this.ClientId = Credentials.ClientId;
				this.ClientSecret = Credentials.ClientSecret;
				this.RedirectURI = Credentials.RedirectURI;
				this.TokenContext = Credentials;
				this.CredentialsFilename = CredentialsFilename;

				if (!DoTokenRefresh())
				{
					this.TokenContext = CreateAccessToken() ?? throw new AuthenticationException("Failed to create an access token!");
				}
			}

			SaveCredentials();
		}
		
		/// <summary>
        /// Reads credentials from the provided file name / path.
        /// </summary>
        /// <param name="FileName">Credentials file name / path.</param>
        /// <returns>An AccessTokenHandler on success. Null on error.</returns>
		public static AccessTokenHandler? FromFile(string FileName)
        {
			try
			{
				if (!File.Exists(FileName)) return null;

				string Content = File.ReadAllText(FileName);
				CredentialsSaveStructure? Credentials = JsonConvert.DeserializeObject<CredentialsSaveStructure>(Content);

				if (Credentials != null) return new AccessTokenHandler(Credentials, FileName);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to read credentials file for MAL Api: " + ex);
			}

			return null;
        }

		/// <summary>
        /// Checks and returns the access token.
        /// </summary>
        /// <returns>Access token.</returns>
		public string GetAccessToken()
		{
			lock (_TokenContextLock)
			{
				//check if token is expired
				if (TokenContext.ExpirationTime <= DateTime.Now)
				{
					DoTokenRefresh();
				}

				return TokenContext.AccessToken;
			}
		}

		/// <summary>
        /// Creates an access token from scratch. Requires user interaction (open browser for MAL login).
        /// </summary>
        /// <returns>MalTokenResponse on success. Null on error.</returns>
		protected MalTokenResponse? CreateAccessToken()
		{
			//obtain "AccessCode" from client/user
			HttpListener Listener = new HttpListener();
			Listener.Prefixes.Add(RedirectURI);
			Listener.Start();

			string CodeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');

			string AuthURL =
				OAuthURL + "authorize" +
				$"?response_type=code" +
				$"&client_id={ClientId}" +
				$"&code_challenge={CodeVerifier}";

			//open browser with url
			Process.Start(new ProcessStartInfo
			{
				FileName = AuthURL,
				UseShellExecute = true
			});

			//after login, await callback from MAL with AccessCode
			string? AccessCode = null;
			while (AccessCode == null)
			{
				HttpListenerContext Context = Listener.GetContext();
				HttpListenerRequest Request = Context.Request;
				HttpListenerResponse Response = Context.Response;

				AccessCode = Request.QueryString["code"];

				Response.StatusCode = 200;
				Response.Close();
			}

			Listener.Stop();

			//exchange AccessCode for AccessToken
			var Client = new HttpClient();

			var PostContent = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = ClientId,
				["client_secret"] = ClientSecret,
				["grant_type"] = "authorization_code",
				["code"] = AccessCode,
				["code_verifier"] = CodeVerifier
			});

			HttpResponseMessage RawPostResponse = Client.PostAsync(OAuthURL + "token", PostContent).Result;

			string PostResponse = RawPostResponse.Content.ReadAsStringAsync().Result;
			MalTokenResponse? Token = JsonConvert.DeserializeObject<MalTokenResponse>(PostResponse);

			if (Token == null || !RawPostResponse.IsSuccessStatusCode)
			{
				Console.WriteLine("Failed to get MAL AccessToken!\n" + PostResponse);
				return null;
			}

			return Token;
		}

		/// <summary>
		/// Refreshes the access token using the refresh token. No user interaction required. Fails if the token is expired for too long.
		/// </summary>
		/// <returns>True on success, false on error.</returns>
		protected bool DoTokenRefresh()
		{
			try
			{
				HttpClient Client = new HttpClient();

				FormUrlEncodedContent Content;
				lock (_TokenContextLock)
				{
					Content = new FormUrlEncodedContent(new Dictionary<string, string>
					{
						["client_id"] = ClientId,
						["client_secret"] = ClientSecret,
						["grant_type"] = "refresh_token",
						["refresh_token"] = TokenContext.RefreshToken
					});
				}

				HttpResponseMessage Response = Client.PostAsync(
					OAuthURL + "token",
					Content
				).Result;

				string ResponseContent = Response.Content.ReadAsStringAsync().Result;

				if (!Response.IsSuccessStatusCode)
				{
					Console.WriteLine($"Failed to refresh Access Token (code {Response.StatusCode}): {ResponseContent}");
					return false;
				}

				MalTokenResponse? NewTokenContext = JsonConvert.DeserializeObject<MalTokenResponse>(ResponseContent);

				if (NewTokenContext != null)
				{
					lock (_TokenContextLock)
					{
						TokenContext = NewTokenContext;
					}

					SaveCredentials();
					return true;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to refresh Access Token: " + ex);
			}

			return false;
		}
		
		/// <summary>
        /// Saves the credentials to a file.
        /// </summary>
        /// <returns>True on success, false on error.</returns>
		protected bool SaveCredentials()
        {
			if (CredentialsFilename == null || CredentialsFilename == string.Empty) return false;

			try
			{
				lock (_TokenContextLock)
				{
					CredentialsSaveStructure Credentials = new CredentialsSaveStructure(ClientId, ClientSecret, RedirectURI, TokenContext);
					string Content = JsonConvert.SerializeObject(Credentials, Formatting.Indented);
					File.WriteAllText(CredentialsFilename, Content);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to save credentials file: " + ex);
				return false;
			}

			return true;
        }

		protected class MalTokenResponse
		{
			private static int RefreshTimeWindowSeconds = 5 * 60;
			public MalTokenResponse()
			{
				AuthenticationTime = DateTime.Now;
			}

			public MalTokenResponse(MalTokenResponse ToCopy)
            {
				AccessToken = ToCopy.AccessToken;
				AuthenticationTime = ToCopy.AuthenticationTime;
				_ExpirationTime = ToCopy._ExpirationTime;
				ExpiresIn = ToCopy.ExpiresIn;
				RefreshToken = ToCopy.RefreshToken;
				TokenType = ToCopy.TokenType;
            }

			[JsonProperty("authentication_time")]
			public DateTime AuthenticationTime { get; }

			[JsonIgnore]
			private DateTime? _ExpirationTime = null;

			[JsonProperty("expiration_time")]
			public DateTime ExpirationTime
			{
				get
				{
					if (_ExpirationTime == null)
					{
						_ExpirationTime = AuthenticationTime.AddSeconds(ExpiresIn - RefreshTimeWindowSeconds);
					}
					return (DateTime)_ExpirationTime;
				}
				private set
                {
					_ExpirationTime = value;
                }
			}

			[JsonProperty("token_type")]
			public string TokenType { get; set; } = "";

			[JsonProperty("expires_in")]
			public int ExpiresIn { get; set; }

			[JsonProperty("access_token")]
			public string AccessToken { get; set; } = "";

			[JsonProperty("refresh_token")]
			public string RefreshToken { get; set; } = "";
		}
		
		protected class CredentialsSaveStructure : MalTokenResponse
		{
			[JsonConstructor]
			private CredentialsSaveStructure() { }

			public CredentialsSaveStructure(string ClientId, string ClientSecret, string RedirectURI, MalTokenResponse TokenContext) : base(TokenContext)
			{
				this.ClientId = ClientId;
				this.ClientSecret = ClientSecret;
				this.RedirectURI = RedirectURI;
			}
			
			[JsonProperty("redirect_uri")]
			public string RedirectURI { get; set; }

			[JsonProperty("client_id")]
			public string ClientId { get; set; }

			[JsonProperty("client_secret")]
			public string ClientSecret { get; set; }
        }
    }
}