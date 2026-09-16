// Purpose: Configures persistence mapping for RetentionExecutionRecord.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class RetentionExecutionRecordConfiguration:IEntityTypeConfiguration<RetentionExecutionRecord>{public void Configure(EntityTypeBuilder<RetentionExecutionRecord>b){b.ToTable("retention_execution_records");b.HasKey(x=>x.Id);b.Property(x=>x.ResourceType).HasMaxLength(120);b.Property(x=>x.Action).HasConversion<string>().HasMaxLength(40);b.Property(x=>x.PolicySnapshotJson).HasMaxLength(32000);b.HasIndex(x=>new{x.OrganizationId,x.ExecutedAtUtc});}}
