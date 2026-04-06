using Microsoft.AspNetCore.Components;
using SIAD.Core.DTOs.Bancos;

public partial class BancoCuentaItem
{
    [Parameter]
    public EventCallback<BancoCuentaListDto> OnSelect { get; set; }

    private Task SelectAsync(BancoCuentaListDto dto)
        => OnSelect.InvokeAsync(dto);
}