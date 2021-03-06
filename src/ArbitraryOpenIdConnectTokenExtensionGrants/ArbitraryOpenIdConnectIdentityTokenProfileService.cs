﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ArbitraryOpenIdConnectTokenExtensionGrants.Extensions;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores.Serialization;
using Newtonsoft.Json;
using ProfileServiceManager;

namespace ArbitraryOpenIdConnectTokenExtensionGrants
{
    public class ArbitraryOpenIdConnectIdentityTokenProfileService : IProfileService,IProfileServicePlugin
    {
        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            if (context.Subject != null)
            {
                var query = from item in context.Subject.Claims
                    where item.Type == "amr"
                    select item.Value;
                var grantType = query.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(grantType) &&
                    string.Compare(grantType,
                        ArbitraryOpenIdConnectTokenExtensionGrants.Constants.ArbitraryOIDCResourceOwner,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var queryClaims = from item in context.Subject.Claims
                        where item.Type == ArbitraryOpenIdConnectTokenExtensionGrants.Constants.ArbitraryClaims
                        select item.Value;
                    var claimsJson = queryClaims.FirstOrDefault();
                    if (claimsJson != null)
                    {
                        var values =
                            JsonConvert.DeserializeObject<Dictionary<string, string>>(claimsJson);
                        // paranoia check.  In no way can we allow creation which tries to spoof someone elses client_id.
                        var qq = from item in values
                            let c = item.Key
                            select c;

                        var queryF = from value in values
                            where String.Compare(value.Key, "client_id", StringComparison.OrdinalIgnoreCase) != 0
                            select value;
                        var trimmedClaims = queryF.ToList();
                        context.IssuedClaims.AddRange(trimmedClaims.Select(value => new Claim(value.Key, value.Value)));
                    }
                }
            }
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {

        }
        public string Name => Constants.ArbitraryOpenIdConnectIdTokenProfileService;
        public IProfileService ProfileService => this;

    }
}