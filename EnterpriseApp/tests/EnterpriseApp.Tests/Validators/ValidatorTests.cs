using EnterpriseApp.Application.DTOs;
using EnterpriseApp.Application.Validators;
using NUnit.Framework;

namespace EnterpriseApp.Tests.Validators;

/// <summary>
/// Unit tests for FluentValidation validators.
/// Ensures all input validation rules are correctly enforced.
/// </summary>
[TestFixture]
public sealed class ValidatorTests
{
    // ── CreateProductRequestValidator ─────────────────────────────────────────

    [Test]
    public async Task CreateProductRequestValidator_ValidRequest_ShouldPassValidation()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Widget", "A great widget", 9.99m, 100);

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task CreateProductRequestValidator_EmptyName_ShouldFail()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest(string.Empty, "Desc", 9.99m, 10);

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "Name"), Is.True);
    }

    [Test]
    public async Task CreateProductRequestValidator_NegativePrice_ShouldFail()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Widget", "Desc", -1m, 10);

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "Price"), Is.True);
    }

    [Test]
    public async Task CreateProductRequestValidator_ZeroPrice_ShouldFail()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Widget", "Desc", 0m, 10);

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task CreateProductRequestValidator_NegativeStock_ShouldFail()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Widget", "Desc", 9.99m, -1);

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "StockQuantity"), Is.True);
    }

    // ── PlaceOrderRequestValidator ────────────────────────────────────────────

    [Test]
    public async Task PlaceOrderRequestValidator_ValidRequest_ShouldPassValidation()
    {
        var validator = new PlaceOrderRequestValidator();
        var request = new PlaceOrderRequest(
            Guid.NewGuid(),
            new[] { new OrderItemRequest(Guid.NewGuid(), 2) });

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task PlaceOrderRequestValidator_EmptyItems_ShouldFail()
    {
        var validator = new PlaceOrderRequestValidator();
        var request = new PlaceOrderRequest(Guid.NewGuid(), Array.Empty<OrderItemRequest>());

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "Items"), Is.True);
    }

    [Test]
    public async Task PlaceOrderRequestValidator_ZeroQuantity_ShouldFail()
    {
        var validator = new PlaceOrderRequestValidator();
        var request = new PlaceOrderRequest(
            Guid.NewGuid(),
            new[] { new OrderItemRequest(Guid.NewGuid(), 0) });

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task PlaceOrderRequestValidator_EmptyCustomerId_ShouldFail()
    {
        var validator = new PlaceOrderRequestValidator();
        var request = new PlaceOrderRequest(
            Guid.Empty,
            new[] { new OrderItemRequest(Guid.NewGuid(), 1) });

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "CustomerId"), Is.True);
    }

    // ── LoginRequestValidator ─────────────────────────────────────────────────

    [Test]
    public async Task LoginRequestValidator_ValidCredentials_ShouldPassValidation()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("admin", "SecurePass1!");

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task LoginRequestValidator_ShortPassword_ShouldFail()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("admin", "short");

        var result = await validator.ValidateAsync(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "Password"), Is.True);
    }
}
