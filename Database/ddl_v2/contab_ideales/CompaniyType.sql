USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CompaniyType]    Script Date: 10/12/2025 13:55:55 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CompaniyType](
	[ID_TypeEntity] [varchar](3) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Descrip2] [varchar](80) NULL,
	[MaskCode] [varchar](25) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [CompaniyType0] PRIMARY KEY CLUSTERED 
(
	[ID_TypeEntity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CompaniyType] ADD  DEFAULT ('X.X.XX.XX.XXX') FOR [MaskCode]
GO

ALTER TABLE [dbo].[CompaniyType] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[CompaniyType]  WITH CHECK ADD CHECK  ((len([MaskCode])>(1)))
GO


