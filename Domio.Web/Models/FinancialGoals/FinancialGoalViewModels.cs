using System.ComponentModel.DataAnnotations;
using Domio.Application.FinancialGoals;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.FinancialGoals;

public sealed class FinancialGoalIndexViewModel
{
    public required FinancialGoalOverview Overview { get; init; }
}

public sealed class FinancialGoalFormViewModel
{
    public Guid? GoalId { get; set; }

    public string ScopeCode { get; set; } = string.Empty;

    public Guid? FamilyGroupId { get; set; }

    public string ScopeNamePl { get; set; } = string.Empty;

    public string OwnerDisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę celu.")]
    [MaxLength(160)]
    [Display(Name = "Nazwa celu")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz kategorię.")]
    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj kwotę docelową.")]
    [Display(Name = "Kwota docelowa")]
    public decimal TargetAmount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Termin celu")]
    public DateTime? TargetDateUtc { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Notatka")]
    public string? Notes { get; set; }

    [Display(Name = "Już odłożono")]
    public decimal? InitialAmount { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
}

public sealed class FinancialGoalContributionViewModel
{
    public Guid GoalId { get; set; }

    public string GoalName { get; set; } = string.Empty;

    public string ScopeCode { get; set; } = string.Empty;

    public Guid? FamilyGroupId { get; set; }

    public string ScopeNamePl { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }

    public decimal SavedAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    [Required(ErrorMessage = "Podaj kwotę wpłaty.")]
    [Display(Name = "Kwota wpłaty")]
    public decimal Amount { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Data wpłaty")]
    public DateTime ContributedAtUtc { get; set; } = DateTime.Today;

    [MaxLength(500)]
    [Display(Name = "Opis")]
    public string? Note { get; set; }
}

public sealed class FinancialGoalDetailsViewModel
{
    public required FinancialGoalDetails Details { get; init; }
}
