﻿using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.ResponseHandling;
using IdentityServer4.Stores;
using IdentityServer4.Validation;
using IdentityServer4Extras;
using IdentityServer4Extras.Services;
using Microsoft.Extensions.Logging;

namespace MultiRefreshTokenSameSubjectSameClientIdWorkAround
{
    public static class TokenTypeHints
    {
        public const string RefreshToken = "refresh_token";
        public const string AccessToken = "access_token";
    }
    /// <summary>
    /// Default revocation response generator
    /// </summary>
    /// <seealso cref="IdentityServer4.ResponseHandling.ITokenRevocationResponseGenerator" />
    public class MyTokenRevocationResponseGenerator : ITokenRevocationResponseGenerator
    {
        /// <summary>
        /// Gets the reference token store.
        /// </summary>
        /// <value>
        /// The reference token store.
        /// </value>
        protected readonly IReferenceTokenStore ReferenceTokenStore;

        /// <summary>
        /// Gets the refresh token store.
        /// </summary>
        /// <value>
        /// The refresh token store.
        /// </value>
        protected readonly IRefreshTokenStore RefreshTokenStore;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        protected readonly ILogger Logger;

        protected readonly ITokenRevocationEventHandler _tokenRevocationEventHandler;
        private ITokenValidator _tokenValidator;
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRevocationResponseGenerator" /> class.
        /// </summary>
        /// <param name="referenceTokenStore">The reference token store.</param>
        /// <param name="refreshTokenStore">The refresh token store.</param>
        /// <param name="logger">The logger.</param>
        public MyTokenRevocationResponseGenerator(
            IReferenceTokenStore referenceTokenStore, 
            IRefreshTokenStore refreshTokenStore,
            ITokenValidator tokenValidator,
            ITokenRevocationEventHandler tokenRevocationEventHandler,
            ILogger<TokenRevocationResponseGenerator> logger)
        {
            ReferenceTokenStore = referenceTokenStore;
            RefreshTokenStore = refreshTokenStore;
            _tokenValidator = tokenValidator;
            _tokenRevocationEventHandler = tokenRevocationEventHandler;
            Logger = logger;
        }

        /// <summary>
        /// Creates the revocation endpoint response and processes the revocation request.
        /// </summary>
        /// <param name="validationResult">The userinfo request validation result.</param>
        /// <returns></returns>
        public virtual async Task<TokenRevocationResponse> ProcessAsync(TokenRevocationRequestValidationResult validationResult)
        {
            var response = new TokenRevocationResponse
            {
                Success = false,
                TokenType = validationResult.TokenTypeHint
            };

            // revoke tokens
            if (validationResult.TokenTypeHint == TokenTypeHints.AccessToken)
            {
                Logger.LogTrace("Hint was for access token");
                response.Success = await RevokeAccessTokenAsync(validationResult);
            }
            else if (validationResult.TokenTypeHint == TokenTypeHints.RefreshToken)
            {
                Logger.LogTrace("Hint was for refresh token");
                response.Success = await RevokeRefreshTokenAsync(validationResult);
            }
            else
            {
                Logger.LogTrace("No hint for token type");

                response.Success = await RevokeAccessTokenAsync(validationResult);

                if (!response.Success)
                {
                    response.Success = await RevokeRefreshTokenAsync(validationResult);
                    response.TokenType = TokenTypeHints.RefreshToken;
                }
                else
                {
                    response.TokenType = TokenTypeHints.AccessToken;
                }
            }

            return response;
        }

        /// <summary>
        /// Revoke access token only if it belongs to client doing the request.
        /// </summary>
        protected virtual async Task<bool> RevokeAccessTokenAsync(TokenRevocationRequestValidationResult validationResult)
        {
            try
            {
                var token = await ReferenceTokenStore.GetReferenceTokenAsync(validationResult.Token);

                string subject;
                if (token != null)
                {
                    subject = token.SubjectId;
                    if (token.ClientId == validationResult.Client.ClientId)
                    {
                        Logger.LogDebug("Access token revoked");
                        await ReferenceTokenStore.RemoveReferenceTokenAsync(validationResult.Token);
                    }
                    else
                    {
                        Logger.LogWarning("Client {clientId} tried to revoke an access token belonging to a different client: {clientId}", validationResult.Client.ClientId, token.ClientId);
                    }
                }
                else
                {
                    var validateAccessToken = await _tokenValidator.ValidateAccessTokenAsync(validationResult.Token);
                    if (validateAccessToken.IsError)
                    {
                        Logger.LogWarning("Client {clientId} access_token not valid: {clientId}", validationResult.Client.ClientId, token.ClientId);
                        return false;
                    }
                    var queryClaims = from item in validateAccessToken.Claims
                        where item.Type == JwtClaimTypes.Subject
                        select item.Value;
                    subject = queryClaims.FirstOrDefault();
                }

                // now we need to revoke this subject
                var rts = RefreshTokenStore as IRefreshTokenStore2;
                await rts.RemoveRefreshTokensAsync(subject, validationResult.Client.ClientId);
                var clientExtra = validationResult.Client as ClientExtra;
                await _tokenRevocationEventHandler.TokenRevokedAsync(clientExtra, subject);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "unexpected error in revocation");
            }

            return false;

        }

        /// <summary>
        /// Revoke refresh token only if it belongs to client doing the request
        /// </summary>
        protected virtual async Task<bool> RevokeRefreshTokenAsync(TokenRevocationRequestValidationResult validationResult)
        {
            var token = await RefreshTokenStore.GetRefreshTokenAsync(validationResult.Token);

            if (token != null)
            {
                if (token.ClientId == validationResult.Client.ClientId)
                {
                    Logger.LogDebug("Refresh token revoked");
                    var rt = await RefreshTokenStore.GetRefreshTokenAsync(validationResult.Token);
                    var rts = RefreshTokenStore as IRefreshTokenStore2;
                    await RefreshTokenStore.RemoveRefreshTokenAsync(validationResult.Token);
                    await rts.RemoveRefreshTokensAsync(token.SubjectId, token.ClientId);
                    await ReferenceTokenStore.RemoveReferenceTokensAsync(token.SubjectId, token.ClientId);
                    var clientExtra = validationResult.Client as ClientExtra;
                    await _tokenRevocationEventHandler.TokenRevokedAsync(clientExtra, token.SubjectId);
                }
                else
                {
                    Logger.LogWarning("Client {clientId} tried to revoke a refresh token belonging to a different client: {clientId}", validationResult.Client.ClientId, token.ClientId);
                }

                return true;
            }

            return false;
        }
    }

}