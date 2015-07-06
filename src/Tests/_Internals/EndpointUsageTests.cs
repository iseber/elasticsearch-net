﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Nest;
using Xunit;

namespace Tests._Internals
{
	public abstract class EndpointUsageTests<TResponse, TInterface, TDescriptor, TInitializer> : SerializationTests
		where TResponse : IResponse
		where TDescriptor : class, TInterface, new() 
		where TInitializer : class, TInterface
		where TInterface : class
	{
		private AsyncLazy<IDictionary<string, TResponse>> _responses;

		public abstract int ExpectStatusCode { get; }
		public abstract bool ExpectIsValid { get; }
		public abstract void AssertUrl(Uri requestUri);

		protected abstract TInitializer Initializer { get; }
		protected abstract Func<TDescriptor, TInterface> Fluent { get; }

		protected abstract void ClientUsage();

		protected EndpointUsageTests()
		{
			// ReSharper disable once DoNotCallOverridableMethodsInConstructor
			this.ClientUsage();
		}

		protected void Calls(
			Func<IElasticClient, Func<TDescriptor, TInterface>, TResponse> fluent,
			Func<IElasticClient, Func<TDescriptor, TInterface>, Task<TResponse>> fluentAsync,
			Func<IElasticClient, TInitializer, TResponse> request,
			Func<IElasticClient, TInitializer, Task<TResponse>> requestAsync
		)
		{
			this._responses = new AsyncLazy<IDictionary<string, TResponse>>(async () =>
			{
				var client = this.GetClient();
				var dict = new Dictionary<string, TResponse>
				{
					{"fluent", fluent(client, this.Fluent)},
					{"fluentAsync", await fluentAsync(client, this.Fluent)},
					{"initializer", request(client, this.Initializer)},
					{"initializerAsync", await requestAsync(client, this.Initializer)}
				};

				return dict;
			});
		}

		protected int IntegrationPort { get; set; } = 9200;
		protected virtual ConnectionSettings GetConnectionSettings(ConnectionSettings settings) => settings; 
		protected virtual IElasticClient GetClient() => TestClient.GetClient(GetConnectionSettings, IntegrationPort); 

		protected async Task AssertOnAllResponses(Action<TResponse> assert)
		{
			var responses = await this._responses;
			foreach (var kv in responses)
			{
				assert(kv.Value);
			}
		}

		[I] protected async void HandlesStatusCode() =>
			await this.AssertOnAllResponses(r=>r.ConnectionStatus.HttpStatusCode.Should().Be(this.ExpectStatusCode));

		[I] protected async void ReturnsExpectedIsValid() =>
			await this.AssertOnAllResponses(r=>r.IsValid.Should().Be(this.ExpectIsValid));

		[U] protected async Task HitsTheCorrectUrl() =>
			await this.AssertOnAllResponses(r=>this.AssertUrl(new Uri(r.ConnectionStatus.RequestUrl)));

		[U] protected void SerializesInitializer() => 
			this.AssertSerializesAndRoundTrips<TInterface>(this.Initializer);
		 
		[U] protected void SerializesFluent() => 
			this.AssertSerializesAndRoundTrips(this.Fluent(new TDescriptor()));

	}
}
