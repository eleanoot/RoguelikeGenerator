﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour {

	// How many cells TALL the maze will be.
	public int mazeRows;

	// how many cells WIDE the maze will be. 
	public int mazeColumns;

	// How large the finished dungeon will be. Calculated from mazeRows and mazeColumns when solution is doubled to space it out.
	private int dungeonRows;
	private int dungeonColumns;

	// How much rock to fill back in. 
	public int sparseness;

	// The chance of a dead end being filled back in. 
	public int removalChance;

	// Currently storing the time since the scene started in here to save processing time with a write to disk for a debug statement. 
	public float totalGenTime;
	public float gridGenTime;
	public float mazeGenTime;
	public float sparseTime;
	public float loopTime;
	public float roomTime;
	public float instantiationTime;

	// Parameters for the number of rooms to include, and their range of possible dimensions. 
	public int noOfRooms;
	public int minWidth;
	public int maxWidth;
	public int minHeight;
	public int maxHeight; 

	// The prefab to use as the cell icon. 
	[SerializeField]
	private GameObject cellPrefab;
	// The tile to use as a wall to fill in dead ends with.
	[SerializeField]
	private GameObject wallPrefab;
	// A tile to differentiate room tiles from the floor.
	[SerializeField]
	private GameObject roomPrefab;

	// Hold and locate all the cells in the maze. 
	private Dictionary<Vector2, Cell> cells = new Dictionary<Vector2, Cell>();
	// Grid pos to spawn pos for use with covering up dead ends.
	private Dictionary<Vector2, Vector2> spawns = new Dictionary<Vector2, Vector2>();
	private List<Cell> walls = new List<Cell> ();
	private List<Cell> rooms = new List<Cell> ();
	private Dictionary<Vector2, Cell> dungeonCells = new Dictionary<Vector2, Cell> ();
	private Dictionary<Vector2, Vector2> dungeonSpawns = new Dictionary<Vector2, Vector2> ();

	// List of unvisited cells.
	private List<Cell> unvisited = new List<Cell>();

	// List to store cells being checked during generation for Recursive Backtracking: the 'stack'.
	private List<Cell> stack = new List<Cell>();

	// Holds the current cell and the next cell being checked.
	private Cell currentCell;
	private Cell checkCell;

	// Array of all possible neighbour positions
	// Left, right, up, down
	private Vector2[] possibleNeighbours = new Vector2[]{new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, -1)};

	// Cell size to determine how far apart to place cells during generation. 
	private float cellSize;

	// Organise the editor hierarchy. 
	private GameObject mazeParent;
	private GameObject wallParent;

	// Checkboxes to select which generation to use.
	public bool recursiveBacktrack;

	// List of all cells found to be dead ends, i.e, 3 walls are active. 
	private List<Cell> deadEnds = new List<Cell> ();

	// Tester to see what rooms is finding
	private GameObject roomParent;

	// Use this for initialization
	void Start () 
	{
		GenerateMaze (mazeRows, mazeColumns);
		//Debug.Log (string.Format ("Time since scene generation: {0}", Time.realtimeSinceStartup)); //probably needs changing from a debug statement as write to disk not obvious and time consuming in itself
		instantiationTime =  Time.realtimeSinceStartup - totalGenTime;
		totalGenTime += instantiationTime;
	}

	private void GenerateMaze (int rows, int columns)
	{
		mazeRows = rows;
		mazeColumns = columns;
		CreateLayout ();
	}

	// Create the grid of cells. 
	public void CreateLayout()
	{
		// Detemrine the size of the cells to place from the tile we're using. 
		cellSize = cellPrefab.transform.localScale.x;

		mazeParent = new GameObject ();
		mazeParent.transform.position = Vector2.zero; 
		mazeParent.name = "Maze";

		// POOLING TESTER
		wallParent = new GameObject ();
		wallParent.transform.position = Vector2.zero; 
		wallParent.name = "Walls";

		// Set the starting point of our maze somewhere in the middle. 
		Vector2 startPos = new Vector2(-(cellSize * (mazeColumns / 2)) + (cellSize / 2), -(cellSize * (mazeRows / 2)) + (cellSize / 2));
		Vector2 spawnPos = startPos;

		for (int x = 0; x < mazeColumns; x++)
		{
			for (int y = 0; y < mazeRows; y++)
			{
				Vector2 gridPos = new Vector2 (x, y);
				GenerateCell(spawnPos, gridPos);
				spawns[gridPos] = spawnPos;

				spawnPos.y += cellSize;
			}


			// Reset spawn position and move up a row. 
			spawnPos.y = startPos.y;
			spawnPos.x += cellSize;
		}

		// Choose a random cell to start from 
		int xStart = Random.Range(0, mazeColumns);
		int yStart = Random.Range (0, mazeRows);
		currentCell = cells [new Vector2 (xStart, yStart)];
		gridGenTime =  Time.realtimeSinceStartup;
		totalGenTime = gridGenTime;

		// Mark our starting cell as visited by removing it from the unvisited list. 
		unvisited.Remove(currentCell);
			
		if (recursiveBacktrack)
			RecursiveBacktracking ();

		mazeGenTime =  Time.realtimeSinceStartup - totalGenTime;
		totalGenTime += mazeGenTime;

		// Fill in the dead ends with some walls.
		FindDeadEnds ();
		FillInEnds();

		sparseTime =  Time.realtimeSinceStartup - totalGenTime;
		totalGenTime += sparseTime;

		// Remove some dead ends by making the maze imperfect.
		RemoveLoops ();

		loopTime =  Time.realtimeSinceStartup - totalGenTime;
		totalGenTime += loopTime;

		//DoubleDungeon ();


		// Place rooms.
	//	roomParent = new GameObject ();
	//	roomParent.transform.position = Vector2.zero; 
	//	roomParent.name = "Rooms";
		PlaceRooms ();
		roomTime =  Time.realtimeSinceStartup - totalGenTime;
		totalGenTime += roomTime;
	}

	// Maze Generation.
	public void RecursiveBacktracking()
	{
		// Loop while there are still cells in the grid we haven't visited. 
		while (unvisited.Count > 0)
		{
			List<Cell> unvisitedNeighbours = GetUnvisitedNeighbours (currentCell);
			if (unvisitedNeighbours.Count > 0)
			{
				// Choose a random unvisited neighbour. 
				checkCell = unvisitedNeighbours[Random.Range(0, unvisitedNeighbours.Count)];
				// Add current cell to stack.
				stack.Add(currentCell);
				// Compare and remove walls if needed.
				CompareWalls(currentCell, checkCell);
				// Make our new current cell the neighbour we were checking. 
				currentCell = checkCell;
				// Mark our new current cell as visited. 
				unvisited.Remove(currentCell);
			}
			else if (stack.Count > 0)
			{
				// Make our current cell the most recently added cell from the stack.
				currentCell = stack[stack.Count - 1];
				// Remove it from the stack.
				stack.Remove(currentCell);
			}
		}
	}

	/* MAZE GENERATION UTILITIES */

	public List<Cell> GetUnvisitedNeighbours(Cell cCell)
	{
		List<Cell> neighbours = new List<Cell> ();
		Cell nCell = cCell;
		// Store the position of our current cell. 
		Vector2 currentPos = cCell.gridPos;

		foreach(Vector2 p in possibleNeighbours)
		{
			// Find the position of a neighbour on the grid, relative to the current cell. 
			Vector2 nPos = currentPos + p;
			// Check the neighbouring cell exists. 
			if (cells.ContainsKey (nPos))
				nCell = cells [nPos];

			// Check if the neighbouring cell is unvisited and thus a valid neighbour. 
			if (unvisited.Contains (nCell))
				neighbours.Add (nCell);
		}

		// Return the completed list of unvisited neighbours.
		return neighbours;
	}

	// Compare current cell with its neighbour and remove walls as appropriate. 
	public void CompareWalls(Cell currentCell, Cell neighbourCell)
	{
		// If neighbour is to the left of current. 
		if (neighbourCell.gridPos.x < currentCell.gridPos.x)
		{
			RemoveWall (neighbourCell.cScript, 2);
			RemoveWall (currentCell.cScript, 1);
		}
		// If neighbour is to the right of current. 
		else if (neighbourCell.gridPos.x > currentCell.gridPos.x)
		{
			RemoveWall (neighbourCell.cScript, 1);
			RemoveWall (currentCell.cScript, 2);
		}
		// If neighbour is above current. 
		else if (neighbourCell.gridPos.y > currentCell.gridPos.y)
		{
			RemoveWall (neighbourCell.cScript, 4);
			RemoveWall (currentCell.cScript, 3);
		}
		// If neighbour is below current. 
		else if (neighbourCell.gridPos.y < currentCell.gridPos.y)
		{
			
			RemoveWall (neighbourCell.cScript, 3);
			RemoveWall (currentCell.cScript, 4);
		}
	}

	// Disables the cell wall chosen by the wallID.
	public void RemoveWall(CellScript cScript, int wallID)
	{
		switch(wallID)
		{
		case 1: 
			// Disable the left wall.
			cScript.wallL.SetActive (false);
			break;
		case 2: 
			// Disable the right wall.
			cScript.wallR.SetActive (false);
			break;
		case 3: 
			// Disable the above wall.
			cScript.wallU.SetActive (false);
			break;
		case 4: 
			// Disable the below wall.
			cScript.wallD.SetActive (false);
			break;
		}
	}

	// Instantiate a cell based on the given position. 
	public void GenerateCell(Vector2 pos, Vector2 keyPos)
	{
		Cell newCell = new Cell ();

		// Store a reference to this position in the grid.
		newCell.gridPos = keyPos;
		// Set and instantiate this cell's GameObject.
		newCell.cellObject = Instantiate(cellPrefab, pos, cellPrefab.transform.rotation);
		// Child the new cell to the maze parent. 
		newCell.cellObject.transform.parent = mazeParent.transform;
		// Set the name of this cellObject.
		newCell.cellObject.name = "Cell - X:" + keyPos.x + " Y:" + keyPos.y;
		// Get reference to attached cell script.
		newCell.cScript = newCell.cellObject.GetComponent<CellScript>();

		// Add this cell to our lists. 
		cells[keyPos] = newCell;
		unvisited.Add (newCell);
	}


	public void FindDeadEnds()
	{
		for (int x = 0; x < mazeColumns; x++)
		{
			for (int y = 0; y < mazeRows; y++)
			{
				Vector2 pos = new Vector2 (x, y);
				if (cells.ContainsKey(pos))
				{
					Cell testCell = cells [pos];
					if (noOfWalls (testCell) >= 3 && !walls.Contains(testCell))
					{
						deadEnds.Add (testCell);
					}
				}
			}
		}
	}

	public void FillInEnds()
	{
		wallParent = new GameObject ();
		wallParent.transform.position = Vector2.zero; 
		wallParent.name = "Walls";
		for (int i = 0; i < sparseness; i++)
		{
			for (int j = 0; j < deadEnds.Count; j++)
			{
				Cell currentEnd = deadEnds [j];

				Vector2 direction = new Vector2(0,0); // The direction vector this passage moves in. 

				// Find the open direction of this cell- the direction this passage is going in. 
				if (!currentEnd.cScript.wallD.activeInHierarchy)
				{
					direction.y -= 1;
				}

				if (!currentEnd.cScript.wallL.activeInHierarchy)
				{
					direction.x -= 1;
				}

				if (!currentEnd.cScript.wallR.activeInHierarchy)
				{
					direction.x += 1;
				}

				if (!currentEnd.cScript.wallU.activeInHierarchy)
				{
					direction.y += 1;
				}

				// Fill in this dead end with a wall. 
				currentEnd.cellObject.SetActive(false);
				currentEnd.cellObject = Instantiate(wallPrefab, spawns[currentEnd.gridPos], wallPrefab.transform.rotation);
				// Child the new cell to the maze parent. 
				cells[currentEnd.gridPos].cellObject.transform.parent = wallParent.transform;
				// Set the name of this cellObject.
				cells[currentEnd.gridPos].cellObject.name = "Cell - X:" + cells[currentEnd.gridPos].gridPos.x + " Y:" + cells[currentEnd.gridPos].gridPos.y;

				walls.Add (currentEnd);

				Vector2 nextPos = deadEnds[j].gridPos + direction; // The position of the next cell we're going to be moving to check. 

				// Follow along ths passageway until we are no longer filling in dead ends in this direction i.e current cell does not have 3 walls.
				while (noOfWalls(cells[nextPos]) == 3 && nextPos.x < mazeRows - 1 && nextPos.y < mazeColumns - 1 && nextPos.x > 0 && nextPos.y > 0)
				{
					// Fill in the current cell if it hasn't been already.
					if (!walls.Contains(cells[nextPos + direction]))
					{
						(cells[nextPos + direction]).cellObject = Instantiate(wallPrefab, spawns[currentEnd.gridPos], wallPrefab.transform.rotation);
						// Child the new cell to the maze parent. 
						cells[nextPos + direction].cellObject.transform.parent = wallParent.transform;
						// Set the name of this cellObject.
						cells[nextPos + direction].cellObject.name = "Cell - X:" + cells[nextPos + direction].gridPos.x + " Y:" + cells[nextPos + direction].gridPos.y;
						walls.Add (cells[nextPos + direction]);

					}
					// Move along the passageway.
					nextPos += direction;
				}


				// Add a wall to the opposite direction on our current non-dead end cell- the way back into the passage we just filled in. 
				if (direction.y == -1)
				{
					cells [nextPos].cScript.wallU.SetActive(true);
				}
				else if (direction.y == 1)
				{
					cells [nextPos].cScript.wallD.SetActive(true);
				}
				else if (direction.x == -1)
				{
					cells [nextPos].cScript.wallR.SetActive(true);
				}
				else if (direction.x == 1)
				{
					cells [nextPos].cScript.wallL.SetActive(true);
				}

				// If our current cell now has 3 walls, it's a dead end, so update our dead end list.
				// else we're done with this dead end, so remove it.
				if (noOfWalls(cells [nextPos]) == 3)
				{
					deadEnds [j] = cells [nextPos];
				}
				else
				{
					deadEnds.RemoveAt(j);
				}
			}
		}

		foreach (KeyValuePair<Vector2, Cell> c in cells)
		{
			if (walls.Contains(c.Value))
			{
				c.Value.cScript.wallL.SetActive(true);
				c.Value.cScript.wallR.SetActive(true);
				c.Value.cScript.wallU.SetActive(true);
				c.Value.cScript.wallD.SetActive(true);
			}
		}


	}

	public Cell ProgressDeadEnd(Cell de)
	{
		Cell nextCell = de;
		if (noOfWalls(de) == 3)
		{
			if (!de.cScript.wallD.activeInHierarchy)
			{
				nextCell.gridPos.y -= 1;
				// Activate the remaining wall of this cell so the next cell has three walls
				nextCell.cScript.wallU.SetActive(true);
			}


			if (!de.cScript.wallL.activeInHierarchy)
			{

				nextCell.gridPos.x -= 1;
				nextCell.cScript.wallR.SetActive(true);
			}

			if (!de.cScript.wallR.activeInHierarchy)
			{

				nextCell.gridPos.x += 1;
				nextCell.cScript.wallL.SetActive(true);
			}

			if (!de.cScript.wallU.activeInHierarchy)
			{

				nextCell.gridPos.y += 1;
				nextCell.cScript.wallD.SetActive(true);
			}
		}

		return nextCell;
	}

	public int noOfWalls(Cell c)
	{
		int count = 0;
		if (c.cScript.wallD.activeInHierarchy)
			count++;

		if (c.cScript.wallL.activeInHierarchy)
			count++;
		if (c.cScript.wallR.activeInHierarchy)
			count++;
		if (c.cScript.wallU.activeInHierarchy)
			count++;

		return count;
	}

	public void RemoveLoops()
	{
	
		// Refind our dead ends since the passages have been filled in.
		deadEnds.Clear();
		FindDeadEnds ();

		for (int i = 0; i < deadEnds.Count; i++)
		{
			bool hitCorridor = false;
			// Roll a number to determine if we're removing this dead end. 
			if (Random.Range(1,101) <= removalChance)
			{
				Cell currentDeadEnd = deadEnds [i];
				// Find current dead end's valid neighbours- cells that are on the grid. 
				while (!hitCorridor)
				{
					List<Cell> neighbours = new List<Cell> ();
					Vector2 currentPos = currentDeadEnd.gridPos;
					// Try to not double back over the cell we just came from, as then will hit a corridor immediately and still have a dead end. 
					int indexToSkip = 0;
					if (!currentDeadEnd.cScript.wallD.activeInHierarchy)
					{
						indexToSkip = 3;
					}
					else if (!currentDeadEnd.cScript.wallL.activeInHierarchy)
					{
						indexToSkip = 0;
					}

					else if (!currentDeadEnd.cScript.wallR.activeInHierarchy)
					{
						indexToSkip = 1;
					}

					else if (!currentDeadEnd.cScript.wallU.activeInHierarchy)
					{
						indexToSkip = 2;
					}

					for (int j = 0; j < possibleNeighbours.Length; j++)
					{
						if (j != indexToSkip)
						{
							// Find the position of a neighbour on the grid, relative to the current cell. 
							Vector2 nPos = currentPos + possibleNeighbours[j];
							// Check the neighbouring cell exists. 
							if (cells.ContainsKey (nPos))
								neighbours.Add(cells [nPos]);
						}
					}

					// Choose a random direction i.e. a random neighbour.
					checkCell = neighbours[Random.Range(0, neighbours.Count)];

					// If this tile was listed as a wall, remove the wall object from it. 
					// Repeat with chosen neighbour as the current 'dead end' tile. 

					if (walls.Contains(checkCell))
					{
						
						checkCell.cellObject.SetActive(false);
						checkCell.cellObject = Instantiate(cellPrefab, spawns[checkCell.gridPos], roomPrefab.transform.rotation);
						walls.Remove (checkCell);
						// Remove walls between chosen neighbour and current dead end. 
						CompareWalls(currentDeadEnd, checkCell);


						currentDeadEnd = checkCell;
					}
					// If not, stop for this dead end. 
					else
					{
						// Remove walls between chosen neighbour and current dead end. 
						CompareWalls(currentDeadEnd, checkCell);
						hitCorridor = true;
					}


				}
			}
		}



	}

	public void DoubleDungeon()
	{
		// Set the dimensions of our dungeon solutions. 
		dungeonRows = mazeRows * 2 + 1;
		dungeonColumns = mazeColumns * 2 + 1;

		// Update existing cells.
		for (int x = 0; x < mazeColumns; x++)
		{
			for (int y = 0; y < mazeRows; y++)
			{
				Cell mazeCell = cells [new Vector2 (x, y)];
				Debug.Log (string.Format ("Maze cell: {0}, {1}", x, y));
				Vector2 oldPos = new Vector2 (mazeCell.gridPos.x, mazeCell.gridPos.y);
				Vector2 oldSpawn = new Vector2 (spawns [mazeCell.gridPos].x, spawns [mazeCell.gridPos].y);

				mazeCell.gridPos = mazeCell.gridPos * 2;
				spawns [mazeCell.gridPos] = new Vector2 (oldSpawn.x + cellSize*x, oldSpawn.y + cellSize*y);

				Debug.Log (string.Format ("Old spawn: {0}, {1}    New spawn: {2}, {3}", oldSpawn.x, oldSpawn.y, spawns[mazeCell.gridPos].x , spawns[mazeCell.gridPos].y ));
				//Vector2 newPos = Vector2.MoveTowards (oldSpawn, spawns [mazeCell.gridPos], Time.deltaTime);
				mazeCell.cellObject.transform.position = spawns [mazeCell.gridPos];

				/*
				Cell mazeCell = cells [new Vector2 (x, y)];
				Debug.Log (string.Format ("Maze cell: {0}, {1}", x, y));
				Cell dungeonCell = new Cell ();
				// Multiply grid position by 2 to add to list of dungeon cells. 
				dungeonCell.gridPos = mazeCell.gridPos * 2;
				dungeonCell.cellObject = mazeCell.cellObject;
				dungeonCell.cScript = mazeCell.cScript;
				// Add one cell size to spawn position for this current grid position.

				Vector2 newSpawn = new Vector2(spawns[mazeCell.gridPos].x + cellSize, spawns[mazeCell.gridPos].y + cellSize);

				// Move existing cell object to new span position. 
				Debug.Log (string.Format ("Old spawn: {0}, {1}    New spawn: {2}, {3}", spawns[mazeCell.gridPos].x , spawns[mazeCell.gridPos].y, newSpawn.x, newSpawn.y));
				Vector2 newPos = Vector2.MoveTowards(spawns[mazeCell.gridPos], newSpawn, Time.deltaTime);
				dungeonCell.cellObject.transform.position = newPos;

				dungeonCells.Add (dungeonCell.gridPos, dungeonCell);
				dungeonSpawns.Add (dungeonCell.gridPos, newSpawn);
				*/
			}
		}

		// Add in new spacer cells.
		/*
		for (int x = 0; x < dungeonColumns; x++)
		{
			for (int y = 0; y < dungeonRows; y++)
			{
				// Add in grid position + 1 for both x and y axis of existing cells in dungeonCells.

				// Add in spawn position + 1 for x and y axis to spawns. 

				// Instantiate these spacer cells. 
			}
		}
		*/
	}

	public void PlaceRooms()
	{
		
		for (int i = 0; i < noOfRooms; i++) {
			// Set our best score to an arbitarily large number.
			int bestScore = int.MaxValue;
			Vector2 bestPos = new Vector2 (0, 0);
			// Generate a random room based on our dimensions to place. 
			int roomWidth = Random.Range (minWidth, maxWidth);
			int roomHeight = Random.Range (minHeight, maxHeight);
			//Debug.Log (string.Format ("Width: {0}, height: {1} for room {2}", roomWidth, roomHeight, i + 1));
			// For each cell C in the dungeon.
			foreach (KeyValuePair<Vector2, Cell> c in cells) 
			{
				// Put bottom left room cell at C.
				Vector2 bottomLeft = c.Key;
				// Calculate the other four corners to use in position checking. 
				Vector2 bottomRight = bottomLeft + new Vector2 (roomWidth, 0);
				Vector2 upperLeft = bottomLeft + new Vector2 (0, roomHeight);
				Vector2 upperRight = upperLeft + new Vector2 (roomWidth, 0);



				//Debug.Log (string.Format ("Upper left: {0}, {1}  Upper right: {2}, {3}   Bottom left: {4}, {5}   Bottom right: {6}, {7}", upperLeft.x, upperLeft.y, upperRight.x, upperRight.y, bottomLeft.x, bottomLeft.y, bottomRight.x, bottomRight.y));
				// Set our current score to 0.
				int currentScore = 0;

				// Only check room position if this position wouldn't take the room completely off the grid. 
				Vector2 cCell = new Vector2(0,0);
				if (upperLeft.y < mazeRows && upperRight.x < mazeColumns)
				{
					// Check every cell that would be covered by the room in this position. 
					// Cover each cell by looping through each x, for each y. 
					for (int x = (int)bottomLeft.x; x < (int)bottomRight.x; x++)
					{
						for (int y = (int)bottomLeft.y; y < (int)upperLeft.y; y++)
						{
					//for (int y = (int)upperLeft.y; y >= (int)bottomLeft.y; y++) {
						//for (int x = (int)upperLeft.x; x <= (int)upperRight.x; x++) {
							cCell = new Vector2 (x, y);
							//Debug.Log (string.Format ("cCell = {0}, {1}", cCell.x, cCell.y));
							// For each cell adjacent to a corridor, add 1 to current score
							// Adjacent = cell is a wall, but is next to at least one floor tile. 

							// Check the current cell's neighbours. 
							Vector2 currentPos = cCell;
							bool isAdjacentC = false;
							for (int j = 0; j < possibleNeighbours.Length; j++) {
								// Find the position of a neighbour on the grid, relative to the current cell. 
								Vector2 nPos = currentPos + possibleNeighbours [j];
								// Check the neighbouring cell exists. 
								// POOLING CHANGES
								if (cells.ContainsKey (nPos) && !walls.Contains(cells[nPos]))
									isAdjacentC = true;
							}
							// For each cell overlapping a room, add 100 to score.
							if (rooms.Contains (cells [cCell])) 
							{
								//Debug.Log (string.Format ("Adding 100 to score."));
								currentScore += 100;
							}
							if (walls.Contains (cells [cCell]) && isAdjacentC) 
							{
								//Debug.Log (string.Format ("Adding 1 to score."));
								currentScore += 1;
							}

							// For each cell overlapping a corridor, add 3 to the score
							if (!walls.Contains (cells [cCell])) 
							{
								//Debug.Log (string.Format ("Adding 3 to score."));
								currentScore += 3;
							}

						}

					}
				}

				//Debug.Log (string.Format ("Current score is {0} for bottom left {1}, {2} for room {3}", currentScore, bottomLeft.x, bottomLeft.y, i+1));
				// If this resulting score for the room is better than our current best, replace it. 
				if (currentScore < bestScore && currentScore > 0) {
					//Debug.Log (string.Format ("Updating best score"));
					bestScore = currentScore;
					bestPos = bottomLeft;
				}

			}
			//Debug.Log (string.Format ("Placing room {0} at pos {1}, {2} with best score {3}", i + 1, bestPos.x, bestPos.y, bestScore));
			// Place the room in the upper left position where the best score was. 
			for (int x = (int)bestPos.x; x < (int)bestPos.x + roomWidth; x++)
			{
				for (int y = (int)bestPos.y; y < (int)bestPos.y + roomHeight; y++)
				{
					
					Cell cCell = cells [new Vector2 (x, y)];
					// Instantiate a room tile here. 
					cCell.cellObject.SetActive(false);
					// Remove all walls
					cCell.cScript.wallD.SetActive(false);
					cCell.cScript.wallL.SetActive(false);
					cCell.cScript.wallR.SetActive(false);
					cCell.cScript.wallU.SetActive(false);
					cCell.cellObject = Instantiate(roomPrefab, spawns[cCell.gridPos], roomPrefab.transform.rotation);

					// Mark this cell as being in a room now. 
					rooms.Add(cCell);



				}
			}

			// For every cell adjacent to a room/corridor, add a doorway. 
		}
		
	}

}