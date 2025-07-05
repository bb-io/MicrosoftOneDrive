using Apps.MicrosoftOneDrive.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.AspNetCore.WebUtilities;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.MicrosoftOneDrive.Invocables;
public class OneDriveInvocable : BaseInvocable
{
    protected AuthenticationCredentialsProvider[] Creds => InvocationContext.AuthenticationCredentialsProviders.ToArray();
    public MicrosoftOneDriveClient Client { get; set; }

    public OneDriveInvocable(InvocationContext invocationContext) : base(invocationContext)
    {
        Client = new MicrosoftOneDriveClient(Creds);
    }


}
