USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01DicJoins]    Script Date: 10/12/2025 13:36:38 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01DicJoins](
	[tablename1] [varchar](60) NOT NULL,
	[tablename2] [varchar](60) NOT NULL,
	[jointype] [varchar](60) NULL,
	[fieldnames1] [varchar](255) NULL,
	[operators] [varchar](60) NULL,
	[fieldnames2] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[tablename1] ASC,
	[tablename2] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


