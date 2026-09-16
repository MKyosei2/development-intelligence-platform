// Purpose: Configures persistence mapping for DistributedIngestionEvent.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class DistributedIngestionEventConfiguration:IEntityTypeConfiguration<DistributedIngestionEvent>{public void Configure(EntityTypeBuilder<DistributedIngestionEvent>b){b.ToTable("distributed_ingestion_events");b.HasKey(x=>x.Id);b.Property(x=>x.IdempotencyKey).HasMaxLength(300);b.Property(x=>x.EnvelopeJson).HasMaxLength(128000);b.Property(x=>x.State).HasConversion<string>().HasMaxLength(40);b.Property(x=>x.LastError).HasMaxLength(8000);b.HasIndex(x=>new{x.OrganizationId,x.IdempotencyKey}).IsUnique();b.HasIndex(x=>new{x.OrganizationId,x.State,x.SourceSequence});}}
