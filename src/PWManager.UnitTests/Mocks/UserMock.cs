using System;
using System.Collections.Generic;
using PWManager.Domain.Vault;

namespace PWManager.UnitTests.Mocks;

public static class UserMock
{
    public static readonly Guid Id1 = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Id2 = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Id3 = new("33333333-3333-3333-3333-333333333333");

    public static List<User> GetUsers() => [new User
    {
        Id = Id1,
        Site = "github.com",
        Login = "dev@github.com",
        Password = "enc_pass_github",
        CreationDate = new DateTime(2024, 1, 10, 8, 0, 0),
        LastUpdated = new DateTime(2024, 3, 15, 9, 30, 0)
    }, new User
    {
        Id = Id2,
        Site = "google.com",
        Login = "user@gmail.com",
        Password = "enc_pass_google",
        CreationDate = new DateTime(2024, 2, 5, 10, 0, 0),
        LastUpdated = new DateTime(2024, 4, 20, 11, 0, 0)
    }, new User
    {
        Id = Id3,
        Site = "amazon.com",
        Login = "buyer@amazon.com",
        Password = "enc_pass_amazon",
        CreationDate = new DateTime(2024, 3, 1, 12, 0, 0),
        LastUpdated = new DateTime(2024, 5, 1, 13, 0, 0)
    }

    ];

    public static User GetSingleUser() => GetUsers()[0];

    public static User GetUserById(Guid id) => GetUsers().Find(u => u.Id == id) ?? throw new InvalidOperationException($"No mock user found with id {id}");
}
