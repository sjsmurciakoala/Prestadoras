USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Config]    Script Date: 10/12/2025 13:30:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Config](
	[siPeriod] [smallint] NOT NULL,
	[ID_Entity] [varchar](10) NOT NULL,
	[EntityName] [varchar](40) NOT NULL,
	[MaskChar] [tinyint] NOT NULL,
	[MaskCode] [varchar](25) NOT NULL,
	[MaskCost] [varchar](15) NOT NULL,
	[CharCr] [tinyint] NOT NULL,
	[MaxAmount] [decimal](28, 2) NOT NULL,
	[boAutoNum] [bit] NOT NULL,
	[boCashFlow] [bit] NOT NULL,
	[boCheckCost] [bit] NOT NULL,
	[boConfig] [bit] NOT NULL,
	[boCurrency] [bit] NOT NULL,
	[boCheckRef] [bit] NOT NULL,
	[boCheckDate] [bit] NOT NULL,
	[boLog] [bit] NOT NULL,
	[boIDRef] [bit] NOT NULL,
	[Frequency] [tinyint] NOT NULL,
	[NextEntry] [int] NOT NULL,
	[NextEntryTemp] [int] NOT NULL,
	[dtLastDepr] [datetime] NULL,
	[NextCertify] [int] NOT NULL,
	[NextDeprec] [int] NOT NULL,
	[NextSheetDL] [int] NOT NULL,
	[NextSheetDG] [int] NOT NULL,
	[NextSheetBS] [int] NOT NULL,
	[NextSheetPL] [int] NOT NULL,
	[PrefixDep] [varchar](4) NOT NULL,
	[ID_Currency] [varchar](3) NULL,
	[ID_AccumProfits] [varchar](24) NULL,
	[ID_AccumDeficit] [varchar](24) NULL,
	[ID_FYProfits] [varchar](24) NULL,
	[ID_FYLoss] [varchar](24) NULL,
	[ID_AccumProfitsX] [varchar](24) NULL,
	[ID_AccumDeficitX] [varchar](24) NULL,
	[ID_FYProfitsX] [varchar](24) NULL,
	[ID_FYLossX] [varchar](24) NULL,
	[TitleProfLoss] [varchar](40) NOT NULL,
	[TitleBalanceSheet] [varchar](40) NOT NULL,
	[AssetDesc] [varchar](40) NOT NULL,
	[LiabilityDesc] [varchar](40) NOT NULL,
	[CapitalDesc] [varchar](40) NOT NULL,
	[LibtyCapital] [varchar](40) NOT NULL,
	[OrderDesc] [varchar](40) NOT NULL,
	[DBVersion] [binary](10) NULL,
 CONSTRAINT [C01Config0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [MaskChar]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('X.X.XX.XX.XXX') FOR [MaskCode]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('X.XX.XXX') FOR [MaskCost]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [CharCr]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((99999999999.99)) FOR [MaxAmount]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boAutoNum]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boCashFlow]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boCheckCost]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boConfig]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boCurrency]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boCheckRef]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boCheckDate]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boLog]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [boIDRef]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((0)) FOR [Frequency]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextEntry]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextEntryTemp]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT (getdate()) FOR [dtLastDepr]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextCertify]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextDeprec]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextSheetDL]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextSheetDG]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextSheetBS]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ((1)) FOR [NextSheetPL]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('Estado de Resultados') FOR [TitleProfLoss]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('Balance General') FOR [TitleBalanceSheet]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('ACTIVO') FOR [AssetDesc]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('PASIVO') FOR [LiabilityDesc]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('CAPITAL') FOR [CapitalDesc]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('PASIVO y CAPITAL') FOR [LibtyCapital]
GO

ALTER TABLE [dbo].[C01Config] ADD  DEFAULT ('CUENTAS ORDEN') FOR [OrderDesc]
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD  CONSTRAINT [C01ConfigFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01Config] CHECK CONSTRAINT [C01ConfigFK01]
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD  CONSTRAINT [C01ConfigFK02] FOREIGN KEY([ID_Entity])
REFERENCES [dbo].[Company] ([ID_Entity])
GO

ALTER TABLE [dbo].[C01Config] CHECK CONSTRAINT [C01ConfigFK02]
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([AssetDesc])=(0) OR len([AssetDesc])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([CapitalDesc])=(0) OR len([CapitalDesc])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([Frequency]>=(0) AND [Frequency]<=(2)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([LiabilityDesc])=(0) OR len([LiabilityDesc])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([LibtyCapital])=(0) OR len([LibtyCapital])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([MaskCode])>(1)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([MaskCost])>(1)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([MaskChar]>=(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextCertify]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextDeprec]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextEntry]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextEntryTemp]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextSheetDL]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextSheetDG]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextSheetBS]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  (([NextSheetPL]>(0)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([OrderDesc])=(0) OR len([OrderDesc])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([TitleProfLoss])=(0) OR len([TitleProfLoss])>=(5)))
GO

ALTER TABLE [dbo].[C01Config]  WITH CHECK ADD CHECK  ((len([TitleBalanceSheet])=(0) OR len([TitleBalanceSheet])>=(5)))
GO


