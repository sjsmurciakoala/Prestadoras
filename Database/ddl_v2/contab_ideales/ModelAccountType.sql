USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[ModelAccountType]    Script Date: 10/12/2025 14:04:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ModelAccountType](
	[ID_TypeEntity] [varchar](3) NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
 CONSTRAINT [ModelAccountType0] PRIMARY KEY CLUSTERED 
(
	[ID_TypeEntity] ASC,
	[ID_Account] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ModelAccountType]  WITH CHECK ADD  CONSTRAINT [ModelAccountTypeFK01] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[ModelAccount] ([ID_Account])
GO

ALTER TABLE [dbo].[ModelAccountType] CHECK CONSTRAINT [ModelAccountTypeFK01]
GO

ALTER TABLE [dbo].[ModelAccountType]  WITH CHECK ADD  CONSTRAINT [ModelAccountTypeFK02] FOREIGN KEY([ID_TypeEntity])
REFERENCES [dbo].[CompaniyType] ([ID_TypeEntity])
GO

ALTER TABLE [dbo].[ModelAccountType] CHECK CONSTRAINT [ModelAccountTypeFK02]
GO

ALTER TABLE [dbo].[ModelAccountType]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[ModelAccountType]  WITH CHECK ADD CHECK  ((len([ID_TypeEntity])<>(0)))
GO


