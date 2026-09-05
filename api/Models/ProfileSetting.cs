using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("ew_prfset")]
public class ProfileSetting
{
    [Key, Column("phcyid", Order = 1)]
    public int ProfileId { get; set; }

    [Key, Column("termid", Order = 2)]
    public short TermId { get; set; }

    [Column("rcp_temp")]
    [MaxLength(100)]
    public string? RcpTemp { get; set; }

    [Column("rcp_print")]
    [MaxLength(50)]
    public string? RcpPrint { get; set; }

    [Column("rcp_prn_sal")]
    public bool? RcpPrnSal { get; set; }

    [Column("ssn_temp")]
    [MaxLength(100)]
    public string? SsnTemp { get; set; }

    [Column("ssn_print")]
    [MaxLength(50)]
    public string? SsnPrint { get; set; }

    [Column("lbl_temp")]
    [MaxLength(100)]
    public string? LblTemp { get; set; }

    [Column("lbl_print")]
    [MaxLength(50)]
    public string? LblPrint { get; set; }

    [Column("bcd_temp")]
    [MaxLength(100)]
    public string? BcdTemp { get; set; }

    [Column("bcd_print")]
    [MaxLength(50)]
    public string? BcdPrint { get; set; }

    [Column("curr1")]
    public short? Curr1 { get; set; }

    [Column("curr2")]
    public short? Curr2 { get; set; }

    [Column("fx_rate")]
    public decimal? FxRate { get; set; }

    [Column("vat_rate")]
    public decimal? VatRate { get; set; }

    [Column("opn_cash")]
    public decimal? OpnCash { get; set; }

    [Column("ssn_rsv")]
    public int? SsnRsv { get; set; }

    [Column("drw_com")]
    [MaxLength(50)]
    public string? DrwCom { get; set; }

    [Column("bck_time")]
    [MaxLength(20)]
    public string? BckTime { get; set; }

    [Column("bck_cnt")]
    public int? BckCnt { get; set; }

    [Column("bck_path")]
    [MaxLength(255)]
    public string? BckPath { get; set; }

    [Column("oper_id")]
    public int? OperId { get; set; }

    [Column("form_id")]
    public int? FormId { get; set; }

    [Column("timest")]
    public DateTime? TimeSt { get; set; }

    [ForeignKey("ProfileId")]
    [InverseProperty("Settings")]
    public Profile Profile { get; set; } = null!;
}
