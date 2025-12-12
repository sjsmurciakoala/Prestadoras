USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01FixedAssest]    Script Date: 10/12/2025 13:37:58 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01FixedAssest](
	[ID_Fix] [varchar](15) NOT NULL,
	[ID_TypeFix] [varchar](6) NOT NULL,
	[ID_DepFix] [varchar](8) NOT NULL,
	[ID_Third] [varchar](30) NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Descrip2] [varchar](80) NULL,
	[ID_LocFix] [varchar](6) NOT NULL,
	[ID_Origin] [varchar](5) NULL,
	[CodProd] [varchar](15) NULL,
	[boDepr] [bit] NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[InServ] [datetime] NOT NULL,
	[Cost] [decimal](28, 2) NOT NULL,
	[Qty] [int] NOT NULL,
	[Salvage] [decimal](28, 2) NOT NULL,
	[Life] [int] NOT NULL,
	[Method] [tinyint] NOT NULL,
	[UndAccum] [decimal](28, 2) NULL,
	[AccumDepr] [decimal](28, 2) NULL,
	[LastDepr] [decimal](28, 2) NULL,
	[dtLastDepr] [datetime] NULL,
	[ID_Currency] [varchar](3) NULL,
	[CurrencyRate] [decimal](28, 9) NULL,
	[boSerial] [bit] NOT NULL,
	[ID_AssetAcct] [varchar](24) NULL,
	[ID_DeprAcct] [varchar](24) NULL,
	[ID_ExpAcct] [varchar](24) NULL,
	[ID_CorrAcct] [varchar](24) NULL,
	[ID_Cost] [varchar](8) NULL,
	[FiscalCost] [decimal](28, 2) NULL,
	[dtFsLastDepr] [datetime] NULL,
	[FiscalAccumDepr] [decimal](28, 2) NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01FixedAssest0] PRIMARY KEY CLUSTERED 
(
	[ID_Fix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [boDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT (getdate()) FOR [InServ]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [Cost]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((1)) FOR [Qty]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [Salvage]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [Life]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((1)) FOR [Method]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [UndAccum]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [AccumDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [LastDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT (getdate()) FOR [dtLastDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [CurrencyRate]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [boSerial]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [FiscalCost]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT (getdate()) FOR [dtFsLastDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [FiscalAccumDepr]
GO

ALTER TABLE [dbo].[C01FixedAssest] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD  CONSTRAINT [C01FixedAssestFK01] FOREIGN KEY([ID_LocFix])
REFERENCES [dbo].[C01FixedLoc] ([ID_LocFix])
GO

ALTER TABLE [dbo].[C01FixedAssest] CHECK CONSTRAINT [C01FixedAssestFK01]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD  CONSTRAINT [C01FixedAssestFK02] FOREIGN KEY([ID_TypeFix])
REFERENCES [dbo].[C01FixedType] ([ID_TypeFix])
GO

ALTER TABLE [dbo].[C01FixedAssest] CHECK CONSTRAINT [C01FixedAssestFK02]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD  CONSTRAINT [C01FixedAssestFK03] FOREIGN KEY([ID_DepFix])
REFERENCES [dbo].[C01FixedDepart] ([ID_DepFix])
GO

ALTER TABLE [dbo].[C01FixedAssest] CHECK CONSTRAINT [C01FixedAssestFK03]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD  CONSTRAINT [C01FixedAssestFK04] FOREIGN KEY([ID_AssetAcct])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01FixedAssest] CHECK CONSTRAINT [C01FixedAssestFK04]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD  CONSTRAINT [C01FixedAssestFK05] FOREIGN KEY([ID_Origin])
REFERENCES [dbo].[C01ConsolOrigin] ([ID_Origin])
GO

ALTER TABLE [dbo].[C01FixedAssest] CHECK CONSTRAINT [C01FixedAssestFK05]
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([AccumDepr]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([FiscalCost]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([FiscalAccumDepr]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  ((len([ID_Origin])<>(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([LastDepr]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([Method]>=(1) AND [Method]<=(4)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([Salvage]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([UndAccum]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([Cost]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([Life]>=(0)))
GO

ALTER TABLE [dbo].[C01FixedAssest]  WITH CHECK ADD CHECK  (([Qty]>=(1)))
GO


