-- PARAMS: {0} = [TABLE NAME]

SELECT TOP 1 *
FROM (SELECT t1.ID + 1 AS ID FROM {0} t1
      WHERE NOT EXISTS(SELECT *
                       FROM {0} t2 
                       WHERE t2.ID = t1.ID + 1)
      UNION 
      SELECT 1 AS ID
      WHERE NOT EXISTS (SELECT *
                        FROM {0} t3
                        WHERE t3.ID = 1)) ot
ORDER BY 1
