using Microsoft.AspNetCore.Mvc;
using Morty.Core.Entities;
using Morty.Core.Repositories;

namespace Morty.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly IProviderRepository _providerRepository;

    public ProvidersController(IProviderRepository providerRepository)
    {
        _providerRepository = providerRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProviders()
    {
        var providers = await _providerRepository.GetAllAsync();
        return Ok(providers.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProviderDto>> GetProvider(int id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            return NotFound();

        return Ok(MapToDto(provider));
    }

    [HttpPost]
    public async Task<ActionResult<ProviderDto>> CreateProvider([FromBody] CreateProviderDto dto)
    {
        var provider = new Provider
        {
            Name = dto.Name,
            Type = dto.Type,
            ApiUrl = dto.ApiUrl,
            Model = dto.Model,
            Token = dto.Token,
            ConfigJson = dto.ConfigJson ?? "{}",
            IsDefault = dto.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _providerRepository.AddAsync(provider);
        return CreatedAtAction(nameof(GetProvider), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProviderDto>> UpdateProvider(int id, [FromBody] UpdateProviderDto dto)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            return NotFound();

        if (dto.Name != null) provider.Name = dto.Name;
        if (dto.Type != null) provider.Type = dto.Type;
        if (dto.ApiUrl != null) provider.ApiUrl = dto.ApiUrl;
        if (dto.Model != null) provider.Model = dto.Model;
        if (dto.Token != null) provider.Token = dto.Token;
        if (dto.ConfigJson != null) provider.ConfigJson = dto.ConfigJson;
        if (dto.IsDefault.HasValue) provider.IsDefault = dto.IsDefault.Value;

        await _providerRepository.UpdateAsync(provider);

        return Ok(MapToDto(provider));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProvider(int id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
            return NotFound();

        await _providerRepository.DeleteAsync(id);
        return NoContent();
    }

    private static ProviderDto MapToDto(Provider provider) => new()
    {
        Id = provider.Id,
        Name = provider.Name,
        Type = provider.Type,
        ApiUrl = provider.ApiUrl,
        Model = provider.Model,
        Token = provider.Token,
        ConfigJson = provider.ConfigJson,
        IsDefault = provider.IsDefault,
        CreatedAt = provider.CreatedAt
    };
}

public class ProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateProviderDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ApiUrl { get; set; }
    public string? Model { get; set; }
    public string? Token { get; set; }
    public string? ConfigJson { get; set; }
    public bool? IsDefault { get; set; }
}
