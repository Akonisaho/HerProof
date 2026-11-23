using System.ComponentModel.DataAnnotations;

namespace HerProof.Web.Models.Repositories;

public class SubmitRepositoryViewModel
{
    [Required]
    [Display(Name = "GitHub repository URL (public)")]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Required]
    public string Branch { get; set; } = "main";
}
