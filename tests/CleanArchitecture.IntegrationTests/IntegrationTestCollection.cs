namespace CleanArchitecture.IntegrationTests;

/// <summary>All database tests share one PostgreSQL container and run sequentially.</summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebAppFactory>;
