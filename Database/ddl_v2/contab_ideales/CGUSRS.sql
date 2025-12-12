USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGUSRS]    Script Date: 10/12/2025 13:55:08 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGUSRS](
	[CodUsua] [varchar](10) NOT NULL,
	[Descrip] [varchar](50) NOT NULL,
	[EMail] [varchar](50) NULL,
	[UsrDta1] [varchar](50) NULL,
	[UsrDta2] [varchar](50) NULL,
	[UsrDta3] [varchar](50) NULL,
	[UsrDta4] [varchar](50) NULL,
	[UsrDta5] [varchar](50) NULL,
	[SData1] [varchar](250) NOT NULL,
	[SData2] [varchar](250) NOT NULL,
	[SData3] [varchar](250) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CodUsua] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


