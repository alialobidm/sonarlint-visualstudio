﻿using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Alm.Authentication
{
    public abstract class Secret
    {
        public static string UriToName(Uri targetUri, string @namespace)
        {
            const string TokenNameBaseFormat = "{0}:{1}://{2}";
            const string TokenNamePortFormat = TokenNameBaseFormat + ":{3}";

            Debug.Assert(targetUri != null, "The targetUri parameter is null");

            Trace.WriteLine("Secret::UriToName");

            string targetName = null;
            // trim any trailing slashes and/or whitespace for compatibility with git-credential-winstore
            string trimmedHostUrl = targetUri.Host
                                             .TrimEnd('/', '\\')
                                             .TrimEnd();

            if (targetUri.IsDefaultPort)
            {
                targetName = String.Format(CultureInfo.InvariantCulture, TokenNameBaseFormat, @namespace, targetUri.Scheme, trimmedHostUrl);
            }
            else
            {
                targetName = String.Format(CultureInfo.InvariantCulture, TokenNamePortFormat, @namespace, targetUri.Scheme, trimmedHostUrl, targetUri.Port);
            }

            Trace.WriteLine("   target name = " + targetName);

            return targetName;
        }

        public delegate string UriNameConversion(Uri targetUri, string @namespace);
    }
}

