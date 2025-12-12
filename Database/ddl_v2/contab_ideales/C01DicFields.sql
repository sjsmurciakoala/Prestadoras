USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01DicFields]    Script Date: 10/12/2025 13:36:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01DicFields](
	[tablename] [varchar](60) NOT NULL,
	[fieldname] [varchar](60) NOT NULL,
	[fieldalias] [varchar](60) NULL,
	[datatype] [varchar](60) NULL,
	[selectable] [char](1) NULL,
	[searchable] [char](1) NULL,
	[sortable] [char](1) NULL,
	[autosearch] [char](1) NULL,
	[mandatory] [char](1) NULL,
PRIMARY KEY CLUSTERED 
(
	[tablename] ASC,
	[fieldname] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


