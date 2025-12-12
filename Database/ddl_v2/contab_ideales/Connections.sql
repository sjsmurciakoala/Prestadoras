USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[Connections]    Script Date: 10/12/2025 13:56:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Connections](
	[ID_Connec] [varchar](10) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[boSSW] [bit] NOT NULL,
	[DBServer] [varchar](50) NOT NULL,
	[DBName] [varchar](40) NOT NULL,
	[DBUser] [varchar](25) NULL,
	[DBPassword] [varchar](25) NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [Connections0] PRIMARY KEY CLUSTERED 
(
	[ID_Connec] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ((0)) FOR [boSSW]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ('REGISTRO NUEVO') FOR [DBServer]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ('REGISTRO NUEVO') FOR [DBName]
GO

ALTER TABLE [dbo].[Connections] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  (([boSSW]=(1) OR [boSSW]=(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  (([boStatus]=(1) OR [boStatus]=(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  ((len([DBName])<>(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  ((len([DBServer])<>(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  ((len([ID_Connec])<>(0)))
GO

ALTER TABLE [dbo].[Connections]  WITH CHECK ADD CHECK  (([siType]=(2) OR [siType]=(1) OR [siType]=(0)))
GO


