USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01FixedType]    Script Date: 10/12/2025 13:39:50 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01FixedType](
	[ID_TypeFix] [varchar](6) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[boDepr] [bit] NOT NULL,
	[ID_AssetAcct] [varchar](24) NULL,
	[ID_DeprAcct] [varchar](24) NULL,
	[ID_ExpAcct] [varchar](24) NULL,
	[ID_CorrAcct] [varchar](24) NULL,
	[ID_Cost] [varchar](8) NULL,
	[boAll] [bit] NOT NULL,
	[Method] [tinyint] NOT NULL,
	[Descrip2] [varchar](80) NULL,
	[Descrip3] [varchar](80) NULL,
	[boByFixed] [bit] NOT NULL,
	[ID_Class] [varchar](5) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01FixedType0] PRIMARY KEY CLUSTERED 
(
	[ID_TypeFix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ((0)) FOR [boDepr]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ((0)) FOR [boAll]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ((1)) FOR [Method]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ((0)) FOR [boByFixed]
GO

ALTER TABLE [dbo].[C01FixedType] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01FixedType]  WITH CHECK ADD CHECK  (([Method]>=(1) AND [Method]<=(4)))
GO


