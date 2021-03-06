﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MWRInstantiate 
{
    // How many cells TALL the maze will be.
    public int mazeRows;

    // how many cells WIDE the maze will be. 
    public int mazeColumns;

    // Cell size to determine how far apart to place cells during generation. 
    private float cellSize;

    
    // Grid pos to spawn pos for use with covering up dead ends.
    private Dictionary<Vector2, Vector2> spawns = new Dictionary<Vector2, Vector2>();
    private List<Cell> walls = new List<Cell>();
    private List<Cell> rooms = new List<Cell>();
    // List of all cells found to be dead ends, i.e, 3 walls are active. 
    private List<Cell> deadEnds = new List<Cell>();

    // List of unvisited cells.
    private List<Cell> unvisited = new List<Cell>();

    // List to store cells being checked during generation for Recursive Backtracking: the 'stack'.
    private List<Cell> stack = new List<Cell>();

    // Holds the current cell and the next cell being checked.
    private Cell currentCell;
    private Cell checkCell;

    public MWRInstantiate(int gridRows, int gridColumns)
    {
        GenerateMaze(gridRows, gridColumns);
        PlacePlayerSpawn();
    }

    private void GenerateMaze(int rows, int columns)
    {
        mazeRows = rows;
        mazeColumns = columns;
        CreateLayout();
    }

    // Create the grid of cells. 
    public void CreateLayout()
    {
        // Detemrine the size of the cells to place from the tile we're using. 
        cellSize = RoguelikeGenerator.instance.cellPrefab.transform.localScale.x;

        // Set the starting point of our maze somewhere in the middle. 
        Vector2 startPos = new Vector2(-(cellSize * (mazeColumns / 2)) + (cellSize / 2), -(cellSize * (mazeRows / 2)) + (cellSize / 2));
        Vector2 spawnPos = startPos;

        for (int x = 0; x < mazeColumns; x++)
        {
            for (int y = 0; y < mazeRows; y++)
            {
                Vector2 gridPos = new Vector2(x, y);
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
        int yStart = Random.Range(0, mazeRows);
        currentCell = RoguelikeGenerator.instance.instantiateCells[new Vector2(xStart, yStart)];
        RoguelikeGenerator.instance.gridGenTime = Time.realtimeSinceStartup;
        RoguelikeGenerator.instance.totalGenTime = RoguelikeGenerator.instance.gridGenTime;

        // Mark our starting cell as visited by removing it from the unvisited list. 
        unvisited.Remove(currentCell);

        
        RecursiveBacktracking();

        RoguelikeGenerator.instance.mazeGenTime = Time.realtimeSinceStartup - RoguelikeGenerator.instance.totalGenTime;
        RoguelikeGenerator.instance.totalGenTime += RoguelikeGenerator.instance.mazeGenTime;

        // Fill in the dead ends with some walls.
        FindDeadEnds();
        FillInEnds();

        RoguelikeGenerator.instance.sparseTime = Time.realtimeSinceStartup - RoguelikeGenerator.instance.totalGenTime;
        RoguelikeGenerator.instance.totalGenTime += RoguelikeGenerator.instance.sparseTime;

        // Remove some dead ends by making the maze imperfect.
        RemoveLoops();

        RoguelikeGenerator.instance.loopTime = Time.realtimeSinceStartup - RoguelikeGenerator.instance.totalGenTime;
        RoguelikeGenerator.instance.totalGenTime += RoguelikeGenerator.instance.loopTime;

        //DoubleDungeon ();


        // Place rooms.
        //	roomParent = new GameObject ();
        //	roomParent.transform.position = Vector2.zero; 
        //	roomParent.name = "Rooms";
        PlaceRooms();
        RoguelikeGenerator.instance.roomTime = Time.realtimeSinceStartup - RoguelikeGenerator.instance.totalGenTime;
        RoguelikeGenerator.instance.totalGenTime += RoguelikeGenerator.instance.roomTime;
    }

    // Instantiate a cell based on the given position. 
    public void GenerateCell(Vector2 pos, Vector2 keyPos)
    {
        Cell newCell = new Cell();

        // Store a reference to this position in the grid.
        newCell.gridPos = keyPos;
        // Set and instantiate this cell's GameObject.
        newCell.cellObject = GameObject.Instantiate(RoguelikeGenerator.instance.cellPrefab, pos, RoguelikeGenerator.instance.cellPrefab.transform.rotation);
        // Child the new cell to the maze parent. 
        newCell.cellObject.transform.parent = RoguelikeGenerator.instance.mazeParent.transform;
        // Set the name of this cellObject.
        newCell.cellObject.name = "Cell - X:" + keyPos.x + " Y:" + keyPos.y;
        // Get reference to attached cell script.
        newCell.cScript = newCell.cellObject.GetComponent<CellScript>();

        // Add this cell to our lists. 
        RoguelikeGenerator.instance.instantiateCells[keyPos] = newCell;
        unvisited.Add(newCell);
    }

    // Maze Generation.
    public void RecursiveBacktracking()
    {
        // Loop while there are still cells in the grid we haven't visited. 
        while (unvisited.Count > 0)
        {
            List<Cell> unvisitedNeighbours = MazeUtils.GetUnvisitedNeighbours(currentCell, ref RoguelikeGenerator.instance.instantiateCells, ref unvisited);
            if (unvisitedNeighbours.Count > 0)
            {
                // Choose a random unvisited neighbour. 
                checkCell = unvisitedNeighbours[Random.Range(0, unvisitedNeighbours.Count)];
                // Add current cell to stack.
                stack.Add(currentCell);
                // Compare and remove walls if needed.
                MazeUtils.CompareWallsInPool(currentCell, checkCell);
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

    public void FindDeadEnds()
    {
        for (int x = 0; x < mazeColumns; x++)
        {
            for (int y = 0; y < mazeRows; y++)
            {
                Vector2 pos = new Vector2(x, y);
                if (RoguelikeGenerator.instance.instantiateCells.ContainsKey(pos))
                {
                    Cell testCell = RoguelikeGenerator.instance.instantiateCells[pos];
                    if (MazeUtils.NoOfWalls(testCell) >= 3 && !walls.Contains(testCell))
                    {
                        deadEnds.Add(testCell);
                    }
                }
            }
        }
    }

    public void FillInEnds()
    {
        for (int i = 0; i < RoguelikeGenerator.instance.sparseness; i++)
        {
            for (int j = 0; j < deadEnds.Count; j++)
            {
                Cell currentEnd = deadEnds[j];

                Vector2 direction = new Vector2(0, 0); // The direction vector this passage moves in. 

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
                currentEnd.cellObject = GameObject.Instantiate(RoguelikeGenerator.instance.wallPrefab, spawns[currentEnd.gridPos], RoguelikeGenerator.instance.transform.rotation);
                // Child the new cell to the maze parent. 
                RoguelikeGenerator.instance.instantiateCells[currentEnd.gridPos].cellObject.transform.parent = RoguelikeGenerator.instance.wallParent.transform;
                // Set the name of this cellObject.
                RoguelikeGenerator.instance.instantiateCells[currentEnd.gridPos].cellObject.name = "Cell - X:" + RoguelikeGenerator.instance.instantiateCells[currentEnd.gridPos].gridPos.x + " Y:" + RoguelikeGenerator.instance.instantiateCells[currentEnd.gridPos].gridPos.y;

                walls.Add(currentEnd);

                Vector2 nextPos = deadEnds[j].gridPos + direction; // The position of the next cell we're going to be moving to check. 

                // Follow along ths passageway until we are no longer filling in dead ends in this direction i.e current cell does not have 3 walls.
                while (MazeUtils.NoOfWalls(RoguelikeGenerator.instance.instantiateCells[nextPos]) == 3 && nextPos.x < mazeRows - 1 && nextPos.y < mazeColumns - 1 && nextPos.x > 0 && nextPos.y > 0)
                {
                    // Fill in the current cell if it hasn't been already.
                    if (!walls.Contains(RoguelikeGenerator.instance.instantiateCells[nextPos + direction]))
                    {
                        (RoguelikeGenerator.instance.instantiateCells[nextPos + direction]).cellObject = GameObject.Instantiate(RoguelikeGenerator.instance.wallPrefab, spawns[currentEnd.gridPos], RoguelikeGenerator.instance.wallPrefab.transform.rotation);
                        // Child the new cell to the maze parent. 
                        RoguelikeGenerator.instance.instantiateCells[nextPos + direction].cellObject.transform.parent = RoguelikeGenerator.instance.wallParent.transform;
                        // Set the name of this cellObject.
                        RoguelikeGenerator.instance.instantiateCells[nextPos + direction].cellObject.name = "Cell - X:" + RoguelikeGenerator.instance.instantiateCells[nextPos + direction].gridPos.x + " Y:" + RoguelikeGenerator.instance.instantiateCells[nextPos + direction].gridPos.y;
                        walls.Add(RoguelikeGenerator.instance.instantiateCells[nextPos + direction]);

                    }
                    // Move along the passageway.
                    nextPos += direction;
                }


                // Add a wall to the opposite direction on our current non-dead end cell- the way back into the passage we just filled in. 
                if (direction.y == -1)
                {
                    RoguelikeGenerator.instance.instantiateCells[nextPos].cScript.wallU.SetActive(true);
                }
                else if (direction.y == 1)
                {
                    RoguelikeGenerator.instance.instantiateCells[nextPos].cScript.wallD.SetActive(true);
                }
                else if (direction.x == -1)
                {
                    RoguelikeGenerator.instance.instantiateCells[nextPos].cScript.wallR.SetActive(true);
                }
                else if (direction.x == 1)
                {
                    RoguelikeGenerator.instance.instantiateCells[nextPos].cScript.wallL.SetActive(true);
                }

                // If our current cell now has 3 walls, it's a dead end, so update our dead end list.
                // else we're done with this dead end, so remove it.
                if (MazeUtils.NoOfWalls(RoguelikeGenerator.instance.instantiateCells[nextPos]) == 3)
                {
                    deadEnds[j] = RoguelikeGenerator.instance.instantiateCells[nextPos];
                }
                else
                {
                    deadEnds.RemoveAt(j);
                }
            }
        }

        foreach (KeyValuePair<Vector2, Cell> c in RoguelikeGenerator.instance.instantiateCells)
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

    public void RemoveLoops()
    {

        // Refind our dead ends since the passages have been filled in.
        deadEnds.Clear();
        FindDeadEnds();

        for (int i = 0; i < deadEnds.Count; i++)
        {
            bool hitCorridor = false;
            // Roll a number to determine if we're removing this dead end. 
            if (Random.Range(1, 101) <= RoguelikeGenerator.instance.removalChance)
            {
                Cell currentDeadEnd = deadEnds[i];
                // Find current dead end's valid neighbours- cells that are on the grid. 
                while (!hitCorridor)
                {
                    List<Cell> neighbours = new List<Cell>();
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

                    for (int j = 0; j < MazeUtils.possibleNeighbours.Length; j++)
                    {
                        if (j != indexToSkip)
                        {
                            // Find the position of a neighbour on the grid, relative to the current cell. 
                            Vector2 nPos = currentPos + MazeUtils.possibleNeighbours[j];
                            // Check the neighbouring cell exists. 
                            if (RoguelikeGenerator.instance.instantiateCells.ContainsKey(nPos))
                                neighbours.Add(RoguelikeGenerator.instance.instantiateCells[nPos]);
                        }
                    }

                    // Choose a random direction i.e. a random neighbour.
                    checkCell = neighbours[Random.Range(0, neighbours.Count)];

                    // If this tile was listed as a wall, remove the wall object from it. 
                    // Repeat with chosen neighbour as the current 'dead end' tile. 

                    if (walls.Contains(checkCell))
                    {

                        checkCell.cellObject.SetActive(false);
                        checkCell.cellObject = GameObject.Instantiate(RoguelikeGenerator.instance.cellPrefab, spawns[checkCell.gridPos], RoguelikeGenerator.instance.roomPrefab.transform.rotation);
                        walls.Remove(checkCell);
                        // Remove walls between chosen neighbour and current dead end. 
                        MazeUtils.CompareWallsInPool(currentDeadEnd, checkCell);


                        currentDeadEnd = checkCell;
                    }
                    // If not, stop for this dead end. 
                    else
                    {
                        // Remove walls between chosen neighbour and current dead end. 
                        MazeUtils.CompareWallsInPool(currentDeadEnd, checkCell);
                        hitCorridor = true;
                    }


                }
            }
        }
    }

    public void PlaceRooms()
    {

        for (int i = 0; i < RoguelikeGenerator.instance.noOfRooms; i++)
        {

            Room r = new Room();
            r.roomNo = i + 1;

            // Set our best score to an arbitarily large number.
            int bestScore = int.MaxValue;
            Vector2 bestPos = new Vector2(0, 0);
            // Generate a random room based on our dimensions to place. 
            int roomWidth = Random.Range(RoguelikeGenerator.instance.minWidth, RoguelikeGenerator.instance.maxWidth);
            int roomHeight = Random.Range(RoguelikeGenerator.instance.minHeight, RoguelikeGenerator.instance.maxHeight);
            //Debug.Log (string.Format ("Width: {0}, height: {1} for room {2}", roomWidth, roomHeight, i + 1));
            // For each cell C in the dungeon.
            foreach (KeyValuePair<Vector2, Cell> c in RoguelikeGenerator.instance.instantiateCells)
            {
                // Put bottom left room cell at C.
                Vector2 bottomLeft = c.Key;
                // Calculate the other four corners to use in position checking. 
                Vector2 bottomRight = bottomLeft + new Vector2(roomWidth, 0);
                Vector2 upperLeft = bottomLeft + new Vector2(0, roomHeight);
                Vector2 upperRight = upperLeft + new Vector2(roomWidth, 0);



                //Debug.Log (string.Format ("Upper left: {0}, {1}  Upper right: {2}, {3}   Bottom left: {4}, {5}   Bottom right: {6}, {7}", upperLeft.x, upperLeft.y, upperRight.x, upperRight.y, bottomLeft.x, bottomLeft.y, bottomRight.x, bottomRight.y));
                // Set our current score to 0.
                int currentScore = 0;

                // Only check room position if this position wouldn't take the room completely off the grid. 
                Vector2 cCell = new Vector2(0, 0);
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
                            cCell = new Vector2(x, y);
                            //Debug.Log (string.Format ("cCell = {0}, {1}", cCell.x, cCell.y));
                            // For each cell adjacent to a corridor, add 1 to current score
                            // Adjacent = cell is a wall, but is next to at least one floor tile. 

                            // Check the current cell's neighbours. 
                            Vector2 currentPos = cCell;
                            bool isAdjacentC = false;
                            for (int j = 0; j < MazeUtils.possibleNeighbours.Length; j++)
                            {
                                // Find the position of a neighbour on the grid, relative to the current cell. 
                                Vector2 nPos = currentPos + MazeUtils.possibleNeighbours[j];
                                // Check the neighbouring cell exists. 
                                // POOLING CHANGES
                                if (RoguelikeGenerator.instance.instantiateCells.ContainsKey(nPos) && !walls.Contains(RoguelikeGenerator.instance.instantiateCells[nPos]))
                                {
                                    isAdjacentC = true;
                                    RoguelikeGenerator.instance.instantiateCells[currentPos].isAdjacentC = true;
                                }
                                    
                            }
                            // For each cell overlapping a room, add 100 to score.
                            if (rooms.Contains(RoguelikeGenerator.instance.instantiateCells[cCell]))
                            {
                                //Debug.Log (string.Format ("Adding 100 to score."));
                                currentScore += 100;
                            }
                            if (walls.Contains(RoguelikeGenerator.instance.instantiateCells[cCell]) && isAdjacentC)
                            {
                                //Debug.Log (string.Format ("Adding 1 to score."));
                                currentScore += 1;
                            }

                            // For each cell overlapping a corridor, add 3 to the score
                            if (!walls.Contains(RoguelikeGenerator.instance.instantiateCells[cCell]))
                            {
                                //Debug.Log (string.Format ("Adding 3 to score."));
                                currentScore += 3;
                            }

                        }

                    }
                }

                //Debug.Log (string.Format ("Current score is {0} for bottom left {1}, {2} for room {3}", currentScore, bottomLeft.x, bottomLeft.y, i+1));
                // If this resulting score for the room is better than our current best, replace it. 
                if (currentScore < bestScore && currentScore > 0)
                {
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

                    Cell cCell = RoguelikeGenerator.instance.instantiateCells[new Vector2(x, y)];

                    if (RoguelikeGenerator.instance.instantiateCells[cCell.gridPos].isAdjacentC)
                        RoguelikeGenerator.instance.adjCellsInstances.Add(RoguelikeGenerator.instance.instantiateCells[cCell.gridPos]);

                    RoguelikeGenerator.instance.instantiateCells[cCell.gridPos].roomNo = i + 1;


                    // Instantiate a room tile here. 
                    cCell.cellObject.SetActive(false);
                    // Remove all walls
                    cCell.cScript.wallD.SetActive(false);
                    cCell.cScript.wallL.SetActive(false);
                    cCell.cScript.wallR.SetActive(false);
                    cCell.cScript.wallU.SetActive(false);
                    cCell.cellObject = GameObject.Instantiate(RoguelikeGenerator.instance.roomPrefab, spawns[cCell.gridPos], RoguelikeGenerator.instance.roomPrefab.transform.rotation);

                    // Mark this cell as being in a room now. 
                    rooms.Add(cCell);

                    r.roomCellsInstantiated.Add(RoguelikeGenerator.instance.instantiateCells[cCell.gridPos]);

                }
            }

            // For every cell adjacent to a room/corridor, add a doorway. 
            RoguelikeGenerator.instance.rooms.Add(r);
        }

        

    }

    // Done in here to swap out a tile instead of passing the lists everywhere.
    public void PlacePlayerSpawn()
    {
        // Choose a random point in a corridor and change that cell's type to the player's spawn. 
        bool validSpawn = false;
        Cell spawnTile = null;
        while (!validSpawn)
        {
            Vector2 pos = new Vector2(Random.Range(0, mazeRows), Random.Range(0, mazeColumns));
            spawnTile = RoguelikeGenerator.instance.instantiateCells[pos];
            if (RoguelikeGenerator.instance.instantiateCells.ContainsKey(pos))
            {
                validSpawn = true;
            }
        }

        // Deactivate the cell here and instantiate a player spawn at its place. 
        spawnTile.cellObject.SetActive(false);
        GameObject.Instantiate(RoguelikeGenerator.instance.spawnPrefab, spawns[spawnTile.gridPos], Quaternion.identity);

        RoguelikeGenerator.instance.playerSpawnInstance = spawnTile;
    }

}
