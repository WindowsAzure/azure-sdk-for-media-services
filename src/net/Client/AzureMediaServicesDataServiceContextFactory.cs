//-----------------------------------------------------------------------
// <copyright file="AzureMediaServicesDataServiceContextFactory.cs" company="Microsoft">Copyright 2012 Microsoft Corporation</copyright>
// <license>
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </license>

using System;
using System.Data.Services.Client;
using System.Data.Services.Common;
using System.Net;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.WindowsAzure.MediaServices.Client.OAuth;
using Microsoft.WindowsAzure.MediaServices.Client.Versioning;

namespace Microsoft.WindowsAzure.MediaServices.Client
{
    /// <summary>
    /// A factory for creating the DataServiceContext connected to Windows Azure Media Services.
    /// </summary>
    public class AzureMediaServicesDataServiceContextFactory
    {
        private readonly Uri _azureMediaServicesEndpoint;
        private readonly OAuthDataServiceAdapter _dataServiceAdapter;
        private readonly ServiceVersionAdapter _serviceVersionAdapter;
        private readonly CloudMediaContext _cloudMediaContext;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureMediaServicesDataServiceContextFactory"/> class.
        /// </summary>
        /// <param name="azureMediaServicesEndpoint">The Windows Azure Media Services endpoint to use.</param>
        /// <param name="dataServiceAdapter">The data service adapter.</param>
        /// <param name="serviceVersionAdapter">The service version adapter.</param>
        /// <param name="cloudMediaContext">The <seealso cref="CloudMediaContext"/> instance.</param>
        public AzureMediaServicesDataServiceContextFactory(Uri azureMediaServicesEndpoint, OAuthDataServiceAdapter dataServiceAdapter, ServiceVersionAdapter serviceVersionAdapter, CloudMediaContext cloudMediaContext)
        {
            this._dataServiceAdapter = dataServiceAdapter;
            this._serviceVersionAdapter = serviceVersionAdapter;
            this._cloudMediaContext = cloudMediaContext;

            this._azureMediaServicesEndpoint = GetAccountApiEndpoint(this._dataServiceAdapter, this._serviceVersionAdapter, azureMediaServicesEndpoint);
        }

        /// <summary>
        /// Creates a data service context.
        /// </summary>
        /// <returns>The new DataServiceContext instance.</returns>
        public IMediaDataServiceContext CreateDataServiceContext()
        {
            DataServiceContext dataContext = new DataServiceContext(_azureMediaServicesEndpoint, DataServiceProtocolVersion.V3)
            {
                IgnoreMissingProperties = true,
                IgnoreResourceNotFoundException = true,
                MergeOption = MergeOption.PreserveChanges,
            };

            this._dataServiceAdapter.Adapt(dataContext);
            this._serviceVersionAdapter.Adapt(dataContext);

            dataContext.ReadingEntity += this.OnReadingEntity;

            return new MediaDataServiceContext(dataContext);
        }

        private static Uri GetAccountApiEndpoint(OAuthDataServiceAdapter dataServiceAdapter, ServiceVersionAdapter versionAdapter, Uri apiServer)
        {
            RetryPolicy retryPolicy = new RetryPolicy(
                new WebRequestTransientErrorDetectionStrategy(),
                RetryStrategyFactory.DefaultStrategy());

            Uri apiEndpoint = null;
            retryPolicy.ExecuteAction(
                    () =>
                        {
                            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(apiServer);
                            request.AllowAutoRedirect = false;
                            dataServiceAdapter.AddAccessTokenToRequest(request);
                            versionAdapter.AddVersionToRequest(request);

                            using (WebResponse response = request.GetResponse())
                            {
                                apiEndpoint = GetAccountApiEndpointFromResponse(response);
                            }
                        }
                );

            return apiEndpoint;
        }

        private static Uri GetAccountApiEndpointFromResponse(WebResponse webResponse)
        {
            HttpWebResponse httpWebResponse = (HttpWebResponse)webResponse;

            if (httpWebResponse.StatusCode == HttpStatusCode.MovedPermanently)
            {
                return new Uri(httpWebResponse.Headers[HttpResponseHeader.Location]);
            }

            if (httpWebResponse.StatusCode == HttpStatusCode.OK)
            {
                return httpWebResponse.ResponseUri;
            }

            throw new InvalidOperationException("Unexpected response code.");
        }
        
        private void OnReadingEntity(object sender, ReadingWritingEntityEventArgs args)
        {
            ICloudMediaContextInit init = args.Entity as ICloudMediaContextInit;
            if (init != null)
            {
                init.InitCloudMediaContext(this._cloudMediaContext);
            }
        }
    }
}