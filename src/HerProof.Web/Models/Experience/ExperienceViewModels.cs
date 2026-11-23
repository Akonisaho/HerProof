using System.ComponentModel.DataAnnotations;
using HerProof.Domain.Experience;

namespace HerProof.Web.Models.Experience;

public class CreateExperienceRecordViewModel
{
    [Required]
    [Display(Name = "Category")]
    public ExperienceCategory Category { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tell us about it")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End date (leave blank if ongoing)")]
    public DateOnly? EndDate { get; set; }
}
