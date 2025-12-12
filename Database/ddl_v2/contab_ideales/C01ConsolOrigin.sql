USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01ConsolOrigin]    Script Date: 10/12/2025 13:31:23 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01ConsolOrigin](
	[ID_Origin] [varchar](5) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[DBServer] [varchar](50) NOT NULL,
	[DBName] [varchar](40) NOT NULL,
	[boSSW] [bit] NOT NULL,
	[DBUser] [varchar](25) NULL,
	[DBPassword] [varchar](25) NULL,
	[Version] [varchar](8) NULL,
	[DBVersion] [binary](10) NULL,
	[LastConsol] [datetime] NULL,
	[LastCant] [smallint] NULL,
	[LastUser] [varchar](50) NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01ConsolOrigin0] PRIMARY KEY CLUSTERED 
(
	[ID_Origin] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ('REGISTRO NUEVO') FOR [DBServer]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ('REGISTRO NUEVO') FOR [DBName]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ((0)) FOR [boSSW]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT (getdate()) FOR [LastConsol]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ((0)) FOR [LastCant]
GO

ALTER TABLE [dbo].[C01ConsolOrigin] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  (([boSSW]=(1) OR [boSSW]=(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  (([boStatus]=(1) OR [boStatus]=(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  ((len([DBName])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  ((len([DBServer])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  ((len([ID_Origin])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolOrigin]  WITH CHECK ADD CHECK  (([siType]=(2) OR [siType]=(1) OR [siType]=(0)))
GO


