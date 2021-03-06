using System;
using System.Collections.Generic;
using IdentityModelExtras;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AspNetCoreIdentityClient.InMemory
{
   
    public static class InMemoryIdentityServiceCollectionExtensions
    {
        public static IdentityBuilder AddAuthentication<TUser>(this IServiceCollection services, IConfiguration configuration)
            where TUser : class => services.AddAuthentication<TUser>(configuration, null);

        public static IdentityBuilder AddAuthentication<TUser>(this IServiceCollection services, 
            IConfiguration configuration, 
            Action<IdentityOptions> setupAction)
            where TUser : class
        {
            // Services used by identity
            var authenticationBuilder = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            });

            var section = configuration.GetSection("oauth2");
            var oAuth2SchemeRecords = new List<OAuth2SchemeRecord>();
            section.Bind(oAuth2SchemeRecords);
            foreach (var record in oAuth2SchemeRecords)
            {
                var scheme = record.Scheme;
                authenticationBuilder.AddOpenIdConnect(scheme, scheme, options =>
                {
                    options.Authority = record.Authority;
                    options.CallbackPath = record.CallbackPath;
                    options.RequireHttpsMetadata = false;

                    options.ClientId = record.ClientId;
                    options.SaveTokens = true;
                    options.Events.OnRedirectToIdentityProvider = async n =>
                    {
                        if (n.ProtocolMessage.RequestType == OpenIdConnectRequestType.Authentication)
                        {
                            n.ProtocolMessage.AcrValues = "v1=google";
                        }
                    };
                });
            }
            
         
            return new IdentityBuilder(typeof(TUser), services);
        }
    }
}