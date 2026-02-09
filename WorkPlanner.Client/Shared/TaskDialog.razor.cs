using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace WorkPlanner.Client.Shared;

public partial class TaskDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
}
