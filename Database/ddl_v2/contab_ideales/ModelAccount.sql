USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[ModelAccount]    Script Date: 10/12/2025 14:04:45 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ModelAccount](
	[ID_Account] [varchar](24) NOT NULL,
	[ID_Parent] [varchar](24) NULL,
	[Descrip] [varchar](80) NOT NULL,
	[DescripShort] [varchar](60) NULL,
	[siLevel] [tinyint] NOT NULL,
	[boStatus] [bit] NOT NULL,
	[dtDate] [datetime] NOT NULL,
 CONSTRAINT [ModelAccount0] PRIMARY KEY CLUSTERED 
(
	[ID_Account] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ModelAccount] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[ModelAccount] ADD  DEFAULT ((1)) FOR [siLevel]
GO

ALTER TABLE [dbo].[ModelAccount] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[ModelAccount] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[ModelAccount]  WITH CHECK ADD CHECK  (([boStatus]=(1) OR [boStatus]=(0)))
GO

ALTER TABLE [dbo].[ModelAccount]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO

ALTER TABLE [dbo].[ModelAccount]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[ModelAccount]  WITH CHECK ADD CHECK  (([siLevel]>=(1)))
GO


